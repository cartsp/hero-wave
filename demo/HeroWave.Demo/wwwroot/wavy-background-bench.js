// wavy-background-bench.js
// Forked renderer for benchmarking — adds optimization toggles and FPS tracking.
// Fork of src/HeroWave/wwwroot/wavy-background.js

// Simplex noise (3D) — Stefan Gustavson's algorithm
const F3 = 1.0 / 3.0;
const G3 = 1.0 / 6.0;

const grad3 = [
    [1,1,0],[-1,1,0],[1,-1,0],[-1,-1,0],
    [1,0,1],[-1,0,1],[1,0,-1],[-1,0,-1],
    [0,1,1],[0,-1,1],[0,1,-1],[0,-1,-1]
];

// FPS buffer sizes at module scope (used in init closures and getFps)
const FPS_BUF = 120;
const FPS_HISTORY = 60;

// Optimization 1: module-level dot/contrib — no closure allocation per noise call
function _dot(g, x, y, z) { return g[0]*x + g[1]*y + g[2]*z; }
function _contrib(g, x, y, z) {
    const t = 0.6 - x*x - y*y - z*z;
    return t < 0 ? 0 : t * t * t * t * _dot(g, x, y, z);
}

function createNoise() {
    const perm = new Uint8Array(512);
    const p = new Uint8Array(256);
    for (let i = 0; i < 256; i++) p[i] = i;
    for (let i = 255; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [p[i], p[j]] = [p[j], p[i]];
    }
    for (let i = 0; i < 512; i++) perm[i] = p[i & 255];

    function skewAndLookup(x, y, z) {
        const s = (x + y + z) * F3;
        const i = Math.floor(x + s);
        const j = Math.floor(y + s);
        const k = Math.floor(z + s);
        const t = (i + j + k) * G3;
        const X0 = i - t, Y0 = j - t, Z0 = k - t;
        const x0 = x - X0, y0 = y - Y0, z0 = z - Z0;
        let i1, j1, k1, i2, j2, k2;
        if (x0 >= y0) {
            if (y0 >= z0) { i1=1;j1=0;k1=0;i2=1;j2=1;k2=0; }
            else if (x0 >= z0) { i1=1;j1=0;k1=0;i2=1;j2=0;k2=1; }
            else { i1=0;j1=0;k1=1;i2=1;j2=0;k2=1; }
        } else {
            if (y0 < z0) { i1=0;j1=0;k1=1;i2=0;j2=1;k2=1; }
            else if (x0 < z0) { i1=0;j1=1;k1=0;i2=0;j2=1;k2=1; }
            else { i1=0;j1=1;k1=0;i2=1;j2=1;k2=0; }
        }
        const x1 = x0-i1+G3, y1 = y0-j1+G3, z1 = z0-k1+G3;
        const x2 = x0-i2+2*G3, y2 = y0-j2+2*G3, z2 = z0-k2+2*G3;
        const x3 = x0-1+3*G3, y3 = y0-1+3*G3, z3 = z0-1+3*G3;
        const ii = i & 255, jj = j & 255, kk = k & 255;
        const gi0 = perm[ii + perm[jj + perm[kk]]] % 12;
        const gi1 = perm[ii+i1 + perm[jj+j1 + perm[kk+k1]]] % 12;
        const gi2 = perm[ii+i2 + perm[jj+j2 + perm[kk+k2]]] % 12;
        const gi3 = perm[ii+1 + perm[jj+1 + perm[kk+1]]] % 12;
        return { gi0,gi1,gi2,gi3, x0,y0,z0, x1,y1,z1, x2,y2,z2, x3,y3,z3 };
    }

    // Unoptimized: inner closure functions allocated on every call
    function noise3D_inner(x, y, z) {
        const r = skewAndLookup(x, y, z);
        function dot(g, x, y, z) { return g[0]*x + g[1]*y + g[2]*z; }
        function contrib(g, x, y, z) {
            const t = 0.6 - x*x - y*y - z*z;
            return t < 0 ? 0 : t * t * t * t * dot(g, x, y, z);
        }
        return 32 * (
            contrib(grad3[r.gi0], r.x0, r.y0, r.z0) +
            contrib(grad3[r.gi1], r.x1, r.y1, r.z1) +
            contrib(grad3[r.gi2], r.x2, r.y2, r.z2) +
            contrib(grad3[r.gi3], r.x3, r.y3, r.z3)
        );
    }

    // Optimized: module-level _dot/_contrib — no allocation per call
    function noise3D_extracted(x, y, z) {
        const r = skewAndLookup(x, y, z);
        return 32 * (
            _contrib(grad3[r.gi0], r.x0, r.y0, r.z0) +
            _contrib(grad3[r.gi1], r.x1, r.y1, r.z1) +
            _contrib(grad3[r.gi2], r.x2, r.y2, r.z2) +
            _contrib(grad3[r.gi3], r.x3, r.y3, r.z3)
        );
    }

    return { inner: noise3D_inner, extracted: noise3D_extracted };
}

// Instance management
const instances = new Map();
let nextId = 0;

export function init(canvas, config) {
    const id = String(nextId++);
    const ctx = canvas.getContext("2d");
    const noiseObj = createNoise();
    let nt = 0;
    let animationFrameId = null;
    let running = true;

    const scale = 0.25;
    const baseW = config.waveWidth;
    const opacityScale = config.opacity / 0.5;

    const layerDefs = [
        { widthMul: 2.4, baseAlpha: 0.02 },
        { widthMul: 2.2, baseAlpha: 0.03 },
        { widthMul: 2.0, baseAlpha: 0.04 },
        { widthMul: 1.8, baseAlpha: 0.05 },
        { widthMul: 1.6, baseAlpha: 0.06 },
        { widthMul: 1.4, baseAlpha: 0.07 },
        { widthMul: 1.2, baseAlpha: 0.08 },
        { widthMul: 1.0, baseAlpha: 0.10 },
        { widthMul: 0.8, baseAlpha: 0.12 },
        { widthMul: 0.6, baseAlpha: 0.14 },
    ];

    const layers = layerDefs.map(l => ({
        width: baseW * l.widthMul * scale,
        alpha: Math.min(1, l.baseAlpha * opacityScale),
    }));

    const step = Math.max(3, Math.round(5 * scale));

    // Optimization flags — all off = baseline behavior matching production renderer
    const opts = {
        extractNoiseFns: false,
        usePath2D: false,
        pixelatedRendering: false,
    };

    // FPS tracking
    const frameTimes = new Float64Array(FPS_BUF);
    let frameIdx = 0;
    let frameCount = 0;
    const fpsHistory = new Int16Array(FPS_HISTORY);
    let histIdx = 0;
    let lastHistorySample = 0;

    function resize() {
        canvas.width = Math.round(canvas.offsetWidth * scale);
        canvas.height = Math.round(canvas.offsetHeight * scale);
    }

    function draw(now) {
        if (!running) return;

        // Record frame timestamp
        frameTimes[frameIdx % FPS_BUF] = now;
        frameIdx++;
        frameCount = Math.min(frameCount + 1, FPS_BUF);

        // Sample FPS history ~every 33ms
        if (now - lastHistorySample >= 33 && frameCount >= 2) {
            const prev = frameTimes[(frameIdx - 2 + FPS_BUF) % FPS_BUF];
            const currentFps = Math.min(999, Math.round(1000 / (now - prev)));
            fpsHistory[histIdx % FPS_HISTORY] = currentFps;
            histIdx++;
            lastHistorySample = now;
        }

        const w = canvas.width;
        const h = canvas.height;
        const halfH = h * 0.5;

        ctx.globalAlpha = 1;
        ctx.fillStyle = config.backgroundColor;
        ctx.fillRect(0, 0, w, h);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        const noise = opts.extractNoiseFns ? noiseObj.extracted : noiseObj.inner;

        for (let i = 0; i < config.waveCount; i++) {
            ctx.strokeStyle = config.colors[i % config.colors.length];

            if (opts.usePath2D) {
                // Optimization 2: Path2D built once per wave per frame, reused for all 10 layers
                const path = new Path2D();
                let first = true;
                for (let x = 0; x < w; x += step) {
                    const y = noise(x / scale / 800, 0.3 * i, nt) * 100 * scale + halfH;
                    if (first) { path.moveTo(x, y); first = false; }
                    else { path.lineTo(x, y); }
                }
                for (const layer of layers) {
                    ctx.globalAlpha = layer.alpha;
                    ctx.lineWidth = layer.width;
                    ctx.stroke(path);
                }
            } else {
                // Baseline: points array + beginPath per layer
                const points = [];
                for (let x = 0; x < w; x += step) {
                    const y = noise(x / scale / 800, 0.3 * i, nt) * 100 * scale + halfH;
                    points.push([x, y]);
                }
                for (const layer of layers) {
                    ctx.globalAlpha = layer.alpha;
                    ctx.lineWidth = layer.width;
                    ctx.beginPath();
                    let first = true;
                    for (const [px, py] of points) {
                        if (first) { ctx.moveTo(px, py); first = false; }
                        else { ctx.lineTo(px, py); }
                    }
                    ctx.stroke();
                }
            }
        }

        ctx.globalAlpha = 1;
        nt += config.speed;
        animationFrameId = requestAnimationFrame(draw);
    }

    resize();
    window.addEventListener("resize", resize);
    animationFrameId = requestAnimationFrame(draw);

    instances.set(id, {
        animationFrameId,
        resize,
        canvas,
        opts,
        frameTimes,
        fpsHistory,
        getFrameIdx: () => frameIdx,
        getFrameCount: () => frameCount,
        getHistIdx: () => histIdx,
        resetFpsData() {
            frameTimes.fill(0);
            fpsHistory.fill(0);
            frameIdx = 0;
            frameCount = 0;
            histIdx = 0;
            lastHistorySample = 0;
        },
        stop: () => { running = false; }
    });
    return id;
}

export function setOptimization(id, key, value) {
    const instance = instances.get(id);
    if (!instance) return;
    instance.opts[key] = value;
    // Optimization 3: toggle CSS image-rendering for cheaper canvas upscaling
    if (key === 'pixelatedRendering') {
        instance.canvas.style.imageRendering = value ? 'pixelated' : 'auto';
    }
}

export function getFps(id) {
    const instance = instances.get(id);
    if (!instance) return { fps: 0, min: 0, max: 0, avg: 0, history: new Array(FPS_HISTORY).fill(0) };

    const fc = instance.getFrameCount();
    const fi = instance.getFrameIdx();
    const hi = instance.getHistIdx();

    let fps = 0;
    if (fc >= 2) {
        const last = instance.frameTimes[(fi - 1 + FPS_BUF) % FPS_BUF];
        const prev = instance.frameTimes[(fi - 2 + FPS_BUF) % FPS_BUF];
        const delta = last - prev;
        fps = delta > 0 ? Math.min(999, Math.round(1000 / delta)) : 0;
    }

    // Build oldest-first history array of length 60
    const history = new Array(FPS_HISTORY);
    const filled = Math.min(hi, FPS_HISTORY);
    for (let i = 0; i < FPS_HISTORY; i++) {
        if (i < FPS_HISTORY - filled) {
            history[i] = 0;
        } else {
            const offset = i - (FPS_HISTORY - filled);
            history[i] = instance.fpsHistory[(hi - filled + offset + FPS_HISTORY) % FPS_HISTORY];
        }
    }

    const nonZero = history.filter(v => v > 0);
    const min = nonZero.length ? Math.min(...nonZero) : 0;
    const max = nonZero.length ? Math.max(...nonZero) : 0;
    const avg = nonZero.length ? Math.round(nonZero.reduce((a, b) => a + b, 0) / nonZero.length) : 0;

    return { fps, min, max, avg, history };
}

export function resetFps(id) {
    const instance = instances.get(id);
    if (instance) instance.resetFpsData();
}

export function dispose(id) {
    const instance = instances.get(id);
    if (!instance) return;
    instance.stop();
    cancelAnimationFrame(instance.animationFrameId);
    window.removeEventListener("resize", instance.resize);
    const ctx = instance.canvas.getContext("2d");
    if (ctx) ctx.clearRect(0, 0, instance.canvas.width, instance.canvas.height);
    instances.delete(id);
}
