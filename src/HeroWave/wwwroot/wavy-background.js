// Simplex noise implementation (3D)
// Based on Stefan Gustavson's implementation
const F3 = 1.0 / 3.0;
const G3 = 1.0 / 6.0;

const grad3 = [
    [1,1,0],[-1,1,0],[1,-1,0],[-1,-1,0],
    [1,0,1],[-1,0,1],[1,0,-1],[-1,0,-1],
    [0,1,1],[0,-1,1],[0,1,-1],[0,-1,-1]
];

// Module-level helpers — avoids closure allocation per noise call
function _dot(g, x, y, z) { return g[0]*x + g[1]*y + g[2]*z; }
function _contrib(g, x, y, z) {
    const t = 0.6 - x*x - y*y - z*z;
    return t < 0 ? 0 : t * t * t * t * _dot(g, x, y, z);
}

function createNoise() {
    const perm = new Uint8Array(512);
    const p = new Uint8Array(256);
    for (let i = 0; i < 256; i++) p[i] = i;
    // Fisher-Yates shuffle
    for (let i = 255; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [p[i], p[j]] = [p[j], p[i]];
    }
    for (let i = 0; i < 512; i++) perm[i] = p[i & 255];

    return function noise3D(x, y, z) {
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

        return 32 * (
            _contrib(grad3[gi0], x0, y0, z0) +
            _contrib(grad3[gi1], x1, y1, z1) +
            _contrib(grad3[gi2], x2, y2, z2) +
            _contrib(grad3[gi3], x3, y3, z3)
        );
    };
}

// Instance management
const instances = new Map();
let nextId = 0;

function debounce(fn, ms) {
    let timer = null;
    const debounced = function (...args) {
        if (timer) clearTimeout(timer);
        timer = setTimeout(() => { timer = null; fn.apply(this, args); }, ms);
    };
    debounced._clear = () => { if (timer) { clearTimeout(timer); timer = null; } };
    return debounced;
}

export function init(canvas, config) {
    const id = String(nextId++);
    const ctx = canvas.getContext("2d");
    if (!ctx) throw new Error("HeroWave: Unable to get 2D rendering context");
    const noise = createNoise();
    let nt = 0;
    let animationFrameId = null;
    let running = true;

    const scale = 0.25;

    // Mutable config — can be updated via update()
    const cfg = {
        waveWidth: config.waveWidth,
        opacity: config.opacity,
        colors: config.colors?.length ? config.colors : ["#38bdf8", "#818cf8", "#c084fc", "#e879f9", "#22d3ee"],
        backgroundColor: config.backgroundColor,
        waveCount: config.waveCount,
        speed: config.speed,
        targetFps: Math.max(1, config.targetFps || 60),
        reducedMotion: config.reducedMotion || 'respectSystemPreference'
    };

    // FPS throttling state
    let lastFrameTime = 0;

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

    let layers = rebuildLayers();

    function rebuildLayers() {
        const opacityScale = cfg.opacity / 0.5;
        return layerDefs.map(l => ({
            width: cfg.waveWidth * l.widthMul * scale,
            alpha: Math.min(1, l.baseAlpha * opacityScale),
        }));
    }

    const step = Math.max(3, Math.round(5 * scale));

    // Reduced-motion support
    const motionQuery = window.matchMedia('(prefers-reduced-motion: reduce)');

    function shouldAnimate() {
        const behavior = cfg.reducedMotion;
        if (behavior === 'alwaysStatic') return false;
        if (behavior === 'alwaysAnimate') return true;
        return !motionQuery.matches;
    }

    function resize() {
        canvas.width = Math.round(canvas.offsetWidth * scale);
        canvas.height = Math.round(canvas.offsetHeight * scale);

        // Redraw static frame when not animating
        if (!animationFrameId) drawFrame();
    }

    function drawFrame() {
        const w = canvas.width;
        const h = canvas.height;

        ctx.globalAlpha = 1;
        ctx.fillStyle = cfg.backgroundColor;
        ctx.fillRect(0, 0, w, h);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        for (let i = 0; i < cfg.waveCount; i++) {
            const path = new Path2D();
            let first = true;
            for (let x = 0; x < w; x += step) {
                const px = x / scale;
                const y = noise(px / 800, 0.3 * i, nt) * 100 * scale + h * 0.5;
                if (first) { path.moveTo(x, y); first = false; }
                else { path.lineTo(x, y); }
            }

            ctx.strokeStyle = cfg.colors[i % cfg.colors.length];
            for (const layer of layers) {
                ctx.globalAlpha = layer.alpha;
                ctx.lineWidth = layer.width;
                ctx.stroke(path);
            }
        }

        ctx.globalAlpha = 1;
        nt += cfg.speed;
    }

    function draw(timestamp) {
        if (!running) return;

        // FPS throttling: skip frame if not enough time has elapsed
        const frameInterval = 1000 / cfg.targetFps;
        if (timestamp - lastFrameTime < frameInterval) {
            animationFrameId = requestAnimationFrame(draw);
            return;
        }
        lastFrameTime = timestamp;

        drawFrame();
        animationFrameId = requestAnimationFrame(draw);
    }

    function startLoop() {
        lastFrameTime = 0;
        animationFrameId = requestAnimationFrame(draw);
    }

    // Debounced resize (100ms)
    const debouncedResize = debounce(resize, 100);

    // Listen for runtime changes to the OS reduced-motion preference
    const motionChangeHandler = () => {
        if (shouldAnimate()) {
            running = true;
            if (!animationFrameId) startLoop();
        } else {
            running = false;
            if (animationFrameId) {
                cancelAnimationFrame(animationFrameId);
                animationFrameId = null;
            }
            drawFrame();
        }
    };
    motionQuery.addEventListener('change', motionChangeHandler);

    // IntersectionObserver: pause when not visible
    const observer = new IntersectionObserver((entries) => {
        for (const entry of entries) {
            if (!entry.isIntersecting) {
                running = false;
                if (animationFrameId) {
                    cancelAnimationFrame(animationFrameId);
                    animationFrameId = null;
                }
            } else {
                if (shouldAnimate()) {
                    running = true;
                    if (!animationFrameId) startLoop();
                } else {
                    drawFrame();
                }
            }
        }
    }, { threshold: 0 });

    observer.observe(canvas);

    resize();
    window.addEventListener("resize", debouncedResize);

    if (shouldAnimate()) {
        startLoop();
    } else {
        drawFrame();
    }

    const instance = {
        canvas,
        config: cfg,
        observer,
        debouncedResize,
        motionQuery,
        motionChangeHandler,
        shouldAnimate,
        rebuildLayers: () => { layers = rebuildLayers(); },
        applyMotionState() {
            if (shouldAnimate()) {
                running = true;
                if (!animationFrameId) startLoop();
            } else {
                running = false;
                if (animationFrameId) {
                    cancelAnimationFrame(animationFrameId);
                    animationFrameId = null;
                }
                drawFrame();
            }
        },
        stop: () => { running = false; },
        get animationFrameId() { return animationFrameId; },
    };

    instances.set(id, instance);
    return id;
}

export function update(id, newConfig) {
    const instance = instances.get(id);
    if (!instance) return;

    const cfg = instance.config;
    const allowedKeys = ['waveWidth', 'opacity', 'colors', 'backgroundColor', 'waveCount', 'speed', 'targetFps', 'reducedMotion'];
    for (const [key, val] of Object.entries(newConfig)) {
        if (allowedKeys.includes(key) && val !== undefined) cfg[key] = val;
    }
    if ('waveWidth' in newConfig || 'opacity' in newConfig) {
        instance.rebuildLayers();
    }
    if ('reducedMotion' in newConfig) {
        instance.applyMotionState();
    }
}

export function dispose(id) {
    const instance = instances.get(id);
    if (!instance) return;

    instance.stop();

    instance.motionQuery.removeEventListener("change", instance.motionChangeHandler);

    if (instance.animationFrameId) {
        cancelAnimationFrame(instance.animationFrameId);
    }

    instance.observer.disconnect();
    instance.debouncedResize._clear();
    window.removeEventListener("resize", instance.debouncedResize);

    const ctx = instance.canvas.getContext("2d");
    if (ctx) ctx.clearRect(0, 0, instance.canvas.width, instance.canvas.height);

    instances.delete(id);
}
