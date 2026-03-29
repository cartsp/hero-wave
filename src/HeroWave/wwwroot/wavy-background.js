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

// Convert any CSS color to "rgba(r, g, b, a)" for gradient color stops.
// Handles 6-digit hex, 3-digit hex, and falls back to canvas parsing.
function colorWithAlpha(color, alpha) {
    // 6-digit hex: #RRGGBB
    if (/^#[0-9a-f]{6}$/i.test(color)) {
        const r = parseInt(color.slice(1, 3), 16);
        const g = parseInt(color.slice(3, 5), 16);
        const b = parseInt(color.slice(5, 7), 16);
        return `rgba(${r},${g},${b},${alpha})`;
    }
    // 3-digit hex: #RGB
    if (/^#[0-9a-f]{3}$/i.test(color)) {
        const r = parseInt(color[1] + color[1], 16);
        const g = parseInt(color[2] + color[2], 16);
        const b = parseInt(color[3] + color[3], 16);
        return `rgba(${r},${g},${b},${alpha})`;
    }
    // For named colors, rgb(), hsl(), etc — use canvas to parse
    const ctx2 = _tmpCtx || (_tmpCtx = document.createElement("canvas").getContext("2d"));
    ctx2.fillStyle = color;
    ctx2.fillRect(0, 0, 1, 1);
    const [r, g, b] = ctx2.getImageData(0, 0, 1, 1).data;
    return `rgba(${r},${g},${b},${alpha})`;
}
let _tmpCtx = null;

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

export function init(canvas, config) {
    const id = String(nextId++);
    const ctx = canvas.getContext("2d");
    const noise = createNoise();
    let nt = 0;
    let animationFrameId = null;
    let running = true;

    const scale = 0.25;

    const baseW = config.waveWidth;
    const opacityScale = config.opacity / 0.5;

    // Guard against empty colors array
    const colors = config.colors?.length ? config.colors : ["#38bdf8", "#818cf8", "#c084fc", "#e879f9", "#22d3ee"];

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

    function resize() {
        canvas.width = Math.round(canvas.offsetWidth * scale);
        canvas.height = Math.round(canvas.offsetHeight * scale);
    }

    function draw() {
        if (!running) return;
        const w = canvas.width;
        const h = canvas.height;
        const amplitude = 100 * scale;
        const yCenter = h * 0.5;

        ctx.globalAlpha = 1;
        ctx.fillStyle = config.backgroundColor;
        ctx.fillRect(0, 0, w, h);
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';

        for (let i = 0; i < config.waveCount; i++) {
            const path = new Path2D();
            let first = true;
            for (let x = 0; x < w; x += step) {
                const px = x / scale;
                const y = noise(px / 800, 0.3 * i, nt) * amplitude + yCenter;
                if (first) { path.moveTo(x, y); first = false; }
                else { path.lineTo(x, y); }
            }

            const color = colors[i % colors.length];

            if (config.gradient === 'vertical') {
                const gradient = ctx.createLinearGradient(0, yCenter - amplitude, 0, yCenter + amplitude);
                gradient.addColorStop(0, colorWithAlpha(color, 0));
                gradient.addColorStop(0.5, colorWithAlpha(color, 0.8));
                gradient.addColorStop(1, colorWithAlpha(color, 0));
                ctx.strokeStyle = gradient;
            } else {
                ctx.strokeStyle = color;
            }
            for (const layer of layers) {
                ctx.globalAlpha = layer.alpha;
                ctx.lineWidth = layer.width;
                ctx.stroke(path);
            }
        }

        ctx.globalAlpha = 1;
        nt += config.speed;
        animationFrameId = requestAnimationFrame(draw);
    }

    resize();
    window.addEventListener("resize", resize);
    animationFrameId = requestAnimationFrame(draw);

    instances.set(id, { animationFrameId, resize, canvas, stop: () => { running = false; } });
    return id;
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
