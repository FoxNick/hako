import {
    init, close, shouldClose, setFPS, beginDraw, endDraw, clear, text, rect,
    begin3D, end3D, cube, isKeyDown, isKeyPressed,
    KEY_W, KEY_A, KEY_S, KEY_D, KEY_SPACE, KEY_Q, KEY_E,
    KEY_UP, KEY_DOWN, KEY_LEFT, KEY_RIGHT, KEY_ENTER
} from 'raylib';

import { init as initAudio, play, shutdown as shutdownAudio } from 'audio';
import { setSeed, preloadAsync, getHeight, clear as clearTerrain } from 'terrain';
import {
    quatIdentity, quatFromEuler, quatFromAxisAngle, quatMultiply, quatRotateVector,
    smoothDampFloat, smoothDampV3, v3Add, v3Scale, v3Length, v3Distance, clamp, degToRad
} from 'math';

const W = 1280, H = 720;
const CHUNK_SIZE = 16, BLOCK_SIZE = 8.0, RENDER_DIST = 3;

const MAX_SPEED = 80;
const THROTTLE_RESPONSE = 8;
const TURN_RATE = 160;
const TURN_RESPONSE = 7;
const RUNG_DISTANCE = 3.0;
const RUNG_TIME_TO_LIVE = 1.8;
const RUNG_COUNT = 24;

const STATE_MENU = 0;
const STATE_LOADING = 1;
const STATE_PLAYING = 2;

const BIOMES = [
    { name: "Alpine Mountains", seed: 12345, desc: "Soaring peaks and deep valleys" },
    { name: "Ocean Expanse", seed: 67890, desc: "Endless blue horizons" },
    { name: "Desert Canyons", seed: 11111, desc: "Red rock formations" }
];

let state = STATE_MENU;
let selected = 0;
let cam: any;
let ship: any;
let chunks = new Map();
let surfaceCache = new Map<string, any[]>();
let dust: any[] = [];
let time = 0;
let soundTimer = 0;
let pendingStart = false;

const DUST_COUNT = 150;
const DUST_EXTENT = 180;

const COL = {
    BLACK: { r: 0, g: 0, b: 0, a: 255 },
    WHITE: { r: 255, g: 255, b: 255, a: 255 },
    GRAY: { r: 120, g: 120, b: 120, a: 255 },
    DARKGRAY: { r: 50, g: 50, b: 50, a: 255 },
    SKYBLUE: { r: 135, g: 206, b: 235, a: 255 },
    ORANGE: { r: 255, g: 160, b: 50, a: 255 },
    GREEN: { r: 80, g: 255, b: 80, a: 255 },
    CYAN: { r: 80, g: 220, b: 255, a: 255 },
    YELLOW: { r: 255, g: 255, b: 100, a: 255 },
    LIGHTGRAY: { r: 180, g: 180, b: 190, a: 255 },
    DARKGREEN: { r: 0, g: 140, b: 100, a: 255 }
};

init(W, H, "Hako Flight Demo - Raylib + TypeScript");
setFPS(60);
initAudio();

function menu(): void {
    if (isKeyPressed(KEY_UP)) selected = (selected - 1 + BIOMES.length) % BIOMES.length;
    if (isKeyPressed(KEY_DOWN)) selected = (selected + 1) % BIOMES.length;
    if ((isKeyPressed(KEY_ENTER) || isKeyPressed(KEY_SPACE)) && !pendingStart) {
        pendingStart = true;
    }

    beginDraw();
    clear(COL.SKYBLUE);

    text("HAKO FLIGHT DEMO", W / 2 - 220, 80, 60, COL.WHITE);
    text("TypeScript + Raylib + Quaternions", W / 2 - 190, 150, 26, COL.GRAY);

    rect(W / 2 - 300, 200, 600, 2, COL.DARKGRAY);

    text("SELECT TERRAIN:", W / 2 - 110, 240, 24, COL.WHITE);

    for (let i = 0; i < BIOMES.length; i++) {
        const y = 290 + i * 75;
        const isSelected = i === selected;
        const bgCol = isSelected ? COL.DARKGRAY : COL.BLACK;
        const txtCol = isSelected ? COL.ORANGE : COL.WHITE;

        rect(W / 2 - 250, y - 8, 500, 65, bgCol);
        text(BIOMES[i].name, W / 2 - 230, y, 30, txtCol);
        text(BIOMES[i].desc, W / 2 - 230, y + 35, 18, COL.GRAY);
    }

    rect(W / 2 - 300, H - 120, 600, 2, COL.DARKGRAY);

    text("PRESS ENTER TO START", W / 2 - 150, H - 90, 22, COL.CYAN);
    text("WASD: Throttle/Strafe  •  Arrows: Pitch/Yaw  •  Q/E: Roll  •  Space: Up",
        W / 2 - 370, H - 50, 16, COL.YELLOW);

    endDraw();
}

async function startGame(): Promise<void> {
    state = STATE_LOADING;
    const biome = BIOMES[selected];

    beginDraw();
    clear(COL.BLACK);
    text("INITIALIZING...", W / 2 - 130, H / 2 - 30, 40, COL.CYAN);
    text(`Loading ${biome.name}`, W / 2 - 150, H / 2 + 30, 24, COL.GRAY);
    endDraw();

    setSeed(biome.seed);
    clearTerrain();
    chunks.clear();
    surfaceCache.clear();

    ship = {
        pos: { x: 0, y: 80, z: 0 },
        vel: { x: 0, y: 0, z: 0 },
        rot: quatFromEuler(0, 0, 0),

        inputFwd: 0, inputLeft: 0, inputUp: 0,
        inputPitch: 0, inputRoll: 0, inputYaw: 0,

        smoothFwd: 0, smoothLeft: 0, smoothUp: 0,
        smoothPitch: 0, smoothRoll: 0, smoothYaw: 0,

        visualBank: 0,

        rungs: Array(RUNG_COUNT).fill(null).map(() => ({
            left: { x: 0, y: 0, z: 0 },
            right: { x: 0, y: 0, z: 0 },
            ttl: 0
        })),
        rungIdx: 0,
        lastRungPos: { x: 0, y: 80, z: 0 }
    };

    dust = [];
    for (let i = 0; i < DUST_COUNT; i++) {
        dust.push({
            pos: {
                x: (Math.random() - 0.5) * DUST_EXTENT * 2,
                y: (Math.random() - 0.5) * DUST_EXTENT * 2,
                z: (Math.random() - 0.5) * DUST_EXTENT * 2
            },
            col: {
                r: Math.floor(220 + Math.random() * 35),
                g: Math.floor(220 + Math.random() * 35),
                b: Math.floor(240 + Math.random() * 15)
            }
        });
    }

    cam = {
        pos: { x: 0, y: 85, z: -18 },
        tar: { x: 0, y: 80, z: 0 },
        up: { x: 0, y: 1, z: 0 },
        fov: 60,
        smoothPos: { x: 0, y: 85, z: -18 },
        smoothTar: { x: 0, y: 80, z: 0 },
        smoothUp: { x: 0, y: 1, z: 0 }
    };

    await preloadAsync(0, 0, RENDER_DIST, CHUNK_SIZE, BLOCK_SIZE);
    loadChunks();
    buildCache();

    state = STATE_PLAYING;
    pendingStart = false;
    play(520, 80, 0.25);
}

function loadChunks(): void {
    const cx = Math.floor(ship.pos.x / (CHUNK_SIZE * BLOCK_SIZE));
    const cz = Math.floor(ship.pos.z / (CHUNK_SIZE * BLOCK_SIZE));

    for (let dx = -RENDER_DIST; dx <= RENDER_DIST; dx++) {
        for (let dz = -RENDER_DIST; dz <= RENDER_DIST; dz++) {
            const key = `${cx + dx},${cz + dz}`;
            if (!chunks.has(key)) {
                chunks.set(key, { cx: cx + dx, cz: cz + dz });
            }
        }
    }

    const toDelete: string[] = [];
    chunks.forEach((_, key) => {
        const [chunkX, chunkZ] = key.split(',').map(Number);
        const dist = Math.max(Math.abs(chunkX - cx), Math.abs(chunkZ - cz));
        if (dist > RENDER_DIST + 1) {
            toDelete.push(key);
            surfaceCache.delete(key);
        }
    });
    toDelete.forEach(k => chunks.delete(k));
}

function buildCache(): void {
    chunks.forEach((chunk, key) => {
        if (!surfaceCache.has(key)) {
            const surfaces: any[] = [];
            const cx = chunk.cx * CHUNK_SIZE * BLOCK_SIZE;
            const cz = chunk.cz * CHUNK_SIZE * BLOCK_SIZE;

            for (let z = 0; z < CHUNK_SIZE; z += 2) {
                for (let x = 0; x < CHUNK_SIZE; x += 2) {
                    const wx = cx + x * BLOCK_SIZE;
                    const wz = cz + z * BLOCK_SIZE;
                    const h = getHeight(wx, wz);

                    let col;
                    if (h < 20) col = { r: 40, g: 90, b: 170 };
                    else if (h < 35) col = { r: 210, g: 190, b: 140 };
                    else if (h < 70) col = { r: 70, g: 150, b: 60 };
                    else if (h < 95) col = { r: 50, g: 110, b: 40 };
                    else if (h < 120) col = { r: 90, g: 90, b: 95 };
                    else col = { r: 245, g: 245, b: 255 };

                    surfaces.push({ x: wx, y: h, z: wz, size: BLOCK_SIZE * 2, col });
                }
            }
            surfaceCache.set(key, surfaces);
        }
    });
}

function getForward(rot: any): any { return quatRotateVector({ x: 0, y: 0, z: 1 }, rot); }
function getRight(rot: any): any { return quatRotateVector({ x: -1, y: 0, z: 0 }, rot); }
function getUp(rot: any): any { return quatRotateVector({ x: 0, y: 1, z: 0 }, rot); }

function transformPoint(point: any, pos: any, rot: any): any {
    return v3Add(quatRotateVector(point, rot), pos);
}

function rotateLocal(rot: any, axis: any, deg: number): any {
    return quatMultiply(rot, quatFromAxisAngle(axis, degToRad(deg)));
}

function updateShip(dt: number): void {
    ship.inputFwd = 0;
    if (isKeyDown(KEY_W)) ship.inputFwd += 1;
    if (isKeyDown(KEY_S)) ship.inputFwd -= 1;

    ship.inputLeft = 0;
    if (isKeyDown(KEY_D)) ship.inputLeft -= 1;
    if (isKeyDown(KEY_A)) ship.inputLeft += 1;

    ship.inputUp = 0;
    if (isKeyDown(KEY_SPACE)) ship.inputUp += 1;

    ship.inputPitch = 0;
    if (isKeyDown(KEY_UP)) ship.inputPitch += 1;
    if (isKeyDown(KEY_DOWN)) ship.inputPitch -= 1;

    ship.inputYaw = 0;
    if (isKeyDown(KEY_LEFT)) ship.inputYaw += 1;
    if (isKeyDown(KEY_RIGHT)) ship.inputYaw -= 1;

    ship.inputRoll = 0;
    if (isKeyDown(KEY_Q)) ship.inputRoll -= 1;
    if (isKeyDown(KEY_E)) ship.inputRoll += 1;

    ship.smoothFwd = smoothDampFloat(ship.smoothFwd, ship.inputFwd, THROTTLE_RESPONSE, dt);
    ship.smoothLeft = smoothDampFloat(ship.smoothLeft, ship.inputLeft, THROTTLE_RESPONSE, dt);
    ship.smoothUp = smoothDampFloat(ship.smoothUp, ship.inputUp, THROTTLE_RESPONSE, dt);

    const fwdMult = ship.smoothFwd > 0 ? 1.0 : 0.33;
    const fwd = getForward(ship.rot);
    const up = getUp(ship.rot);
    const left = getRight(ship.rot);

    let targetVel = { x: 0, y: 0, z: 0 };
    targetVel = v3Add(targetVel, v3Scale(fwd, MAX_SPEED * fwdMult * ship.smoothFwd));
    targetVel = v3Add(targetVel, v3Scale(up, MAX_SPEED * 0.5 * ship.smoothUp));
    targetVel = v3Add(targetVel, v3Scale(left, MAX_SPEED * 0.5 * ship.smoothLeft));

    ship.vel = smoothDampV3(ship.vel, targetVel, 3.5, dt);
    ship.pos = v3Add(ship.pos, v3Scale(ship.vel, dt));

    ship.smoothPitch = smoothDampFloat(ship.smoothPitch, ship.inputPitch, TURN_RESPONSE, dt);
    ship.smoothRoll = smoothDampFloat(ship.smoothRoll, ship.inputRoll, TURN_RESPONSE, dt);
    ship.smoothYaw = smoothDampFloat(ship.smoothYaw, ship.inputYaw, TURN_RESPONSE, dt);

    ship.rot = rotateLocal(ship.rot, { x: 0, y: 0, z: 1 }, ship.smoothRoll * TURN_RATE * dt);
    ship.rot = rotateLocal(ship.rot, { x: 1, y: 0, z: 0 }, ship.smoothPitch * TURN_RATE * dt);
    ship.rot = rotateLocal(ship.rot, { x: 0, y: 1, z: 0 }, ship.smoothYaw * TURN_RATE * dt);

    const forwardVec = getForward(ship.rot);
    if (Math.abs(forwardVec.y) < 0.8) {
        const rightVec = getRight(ship.rot);
        ship.rot = rotateLocal(ship.rot, { x: 0, y: 0, z: 1 }, rightVec.y * TURN_RATE * 0.5 * dt);
    }

    const targetBank = degToRad(-30 * ship.smoothYaw - 15 * ship.smoothLeft);
    ship.visualBank = smoothDampFloat(ship.visualBank, targetBank, 10, dt);

    updateTrail(dt);

    const terrainH = getHeight(ship.pos.x, ship.pos.z);
    if (ship.pos.y < terrainH + 3) {
        ship.pos.y = terrainH + 3;
        ship.vel.y = Math.max(0, ship.vel.y);
    }
}

function updateTrail(dt: number): void {
    ship.rungs[ship.rungIdx].ttl = RUNG_TIME_TO_LIVE;
    const halfW = 0.5, halfL = 0.5;
    ship.rungs[ship.rungIdx].left = transformPoint({ x: -halfW, y: 0, z: -halfL }, ship.pos, ship.rot);
    ship.rungs[ship.rungIdx].right = transformPoint({ x: halfW, y: 0, z: -halfL }, ship.pos, ship.rot);

    if (v3Distance(ship.pos, ship.lastRungPos) > RUNG_DISTANCE) {
        ship.rungIdx = (ship.rungIdx + 1) % RUNG_COUNT;
        ship.lastRungPos = { ...ship.pos };
    }

    for (let i = 0; i < RUNG_COUNT; i++) {
        ship.rungs[i].ttl -= dt;
    }
}

function updateCamera(dt: number): void {
    const camPos = transformPoint({ x: 0, y: 2, z: -6 }, ship.pos, ship.rot);
    const lookAhead = v3Scale(getForward(ship.rot), 35);
    const camTar = v3Add(ship.pos, lookAhead);
    const camUp = getUp(ship.rot);

    cam.smoothPos = smoothDampV3(cam.smoothPos, camPos, 12, dt);
    cam.smoothTar = smoothDampV3(cam.smoothTar, camTar, 6, dt);
    cam.smoothUp = smoothDampV3(cam.smoothUp, camUp, 6, dt);

    cam.pos = cam.smoothPos;
    cam.tar = cam.smoothTar;
    cam.up = cam.smoothUp;
}

function updateDust(): void {
    const viewPos = cam.pos;
    const size = DUST_EXTENT * 2;

    for (const d of dust) {
        while (d.pos.x > viewPos.x + DUST_EXTENT) d.pos.x -= size;
        while (d.pos.x < viewPos.x - DUST_EXTENT) d.pos.x += size;
        while (d.pos.y > viewPos.y + DUST_EXTENT) d.pos.y -= size;
        while (d.pos.y < viewPos.y - DUST_EXTENT) d.pos.y += size;
        while (d.pos.z > viewPos.z + DUST_EXTENT) d.pos.z -= size;
        while (d.pos.z < viewPos.z - DUST_EXTENT) d.pos.z += size;
    }
}

function updateSound(): void {
    soundTimer += 0.016;
    if (soundTimer >= 0.1) {
        soundTimer = 0;
        const spd = v3Length(ship.vel);
        const freq = 80 + spd * 2;
        const vol = 0.04 + (spd / MAX_SPEED) * 0.06;
        play(Math.floor(freq), 50, vol);
    }
}

function drawSky(): void {
    const horizon = H / 2;
    const step = 6;

    for (let y = 0; y < horizon; y += step) {
        const t = y / horizon;
        const r = Math.floor(20 + (135 - 20) * t);
        const g = Math.floor(60 + (206 - 60) * t);
        const b = Math.floor(140 + (235 - 140) * t);
        rect(0, y, W, step, { r, g, b, a: 255 });
    }

    for (let y = horizon; y < H; y += step) {
        const t = (y - horizon) / (H - horizon);
        const r = Math.floor(135 - (135 - 110) * t);
        const g = Math.floor(206 - (206 - 130) * t);
        const b = Math.floor(235 - (235 - 90) * t);
        rect(0, y, W, step, { r, g, b, a: 255 });
    }
}

function drawShip(): void {
    const visRot = quatMultiply(ship.rot, quatFromAxisAngle({ x: 0, y: 0, z: 1 }, ship.visualBank));

    cube(ship.pos, 0.8, 0.5, 2.0, { r: 220, g: 225, b: 235, a: 255 });

    const wingOff = quatRotateVector({ x: 0, y: -0.1, z: 0 }, visRot);
    const wingPos = v3Add(ship.pos, wingOff);
    cube(wingPos, 4.0, 0.12, 1.0, { r: 200, g: 205, b: 215, a: 255 });

    const tailOff = quatRotateVector({ x: 0, y: 0.6, z: 1.1 }, visRot);
    const tailPos = v3Add(ship.pos, tailOff);
    cube(tailPos, 0.2, 1.0, 0.5, { r: 200, g: 205, b: 215, a: 255 });

    const noseOff = quatRotateVector({ x: 0, y: 0, z: -1.1 }, visRot);
    const nosePos = v3Add(ship.pos, noseOff);
    cube(nosePos, 0.5, 0.3, 0.4, { r: 180, g: 185, b: 195, a: 255 });
}

function drawTrail(): void {
    for (let i = 0; i < RUNG_COUNT; i++) {
        const rung = ship.rungs[i];
        if (rung.ttl <= 0) continue;

        const alpha = Math.floor(180 * rung.ttl / RUNG_TIME_TO_LIVE);
        const col = { r: 80, g: 220, b: 200, a: alpha };

        if (i !== ship.rungIdx) {
            drawLine(rung.left, rung.right, col);
        }

        const nextIdx = (i + 1) % RUNG_COUNT;
        const nextRung = ship.rungs[nextIdx];
        if (nextRung.ttl > 0 && rung.ttl < nextRung.ttl) {
            drawLine(nextRung.left, rung.left, col);
            drawLine(nextRung.right, rung.right, col);
        }
    }
}

function drawLine(start: any, end: any, col: any): void {
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const dz = end.z - start.z;
    const segs = 5;

    for (let i = 0; i <= segs; i++) {
        const t = i / segs;
        const pos = { x: start.x + dx * t, y: start.y + dy * t, z: start.z + dz * t };
        cube(pos, 0.08, 0.08, 0.08, col);
    }
}

function drawDust(): void {
    for (const d of dust) {
        const dist = v3Distance(cam.pos, d.pos);
        const fadeStart = DUST_EXTENT * 0.85;
        const fadeEnd = DUST_EXTENT;
        const alpha = Math.floor(255 * clamp(1 - (dist - fadeStart) / (fadeEnd - fadeStart), 0, 1));

        if (alpha > 15) {
            const trailStart = v3Add(d.pos, v3Scale(ship.vel, 0.025));
            drawLine(trailStart, d.pos, { ...d.col, a: alpha });
        }
    }
}

function renderGame(): void {
    beginDraw();
    clear(COL.BLACK);
    drawSky();

    begin3D(cam);

    const maxDist = 220 * 220;
    surfaceCache.forEach((surfaces) => {
        surfaces.forEach((block) => {
            const dx = block.x - ship.pos.x;
            const dz = block.z - ship.pos.z;
            if (dx * dx + dz * dz < maxDist) {
                cube({ x: block.x, y: block.y / 2, z: block.z },
                    block.size, block.y, block.size, { ...block.col, a: 255 });
            }
        });
    });

    drawShip();
    drawTrail();
    drawDust();

    end3D();

    rect(15, 15, 330, 220, { r: 0, g: 0, b: 0, a: 200 });

    text("VELOCITY", 30, 30, 18, COL.GRAY);
    const spd = v3Length(ship.vel);
    const spdKmh = spd * 3.6;
    text(`${spdKmh.toFixed(0)} km/h`, 30, 52, 32, COL.CYAN);

    text("ALTITUDE", 30, 100, 18, COL.GRAY);
    text(`${ship.pos.y.toFixed(0)} m`, 30, 122, 32, COL.WHITE);

    text("THROTTLE", 30, 170, 16, COL.GRAY);
    const throttleBar = (ship.smoothFwd + 1) * 0.5;
    rect(30, 195, 140, 20, COL.DARKGRAY);
    const throttleCol = throttleBar > 0.5 ? COL.GREEN : COL.ORANGE;
    rect(30, 195, throttleBar * 140, 20, throttleCol);
    text(`${Math.floor(throttleBar * 100)}%`, 180, 197, 18, COL.WHITE);

    rect(W - 345, 15, 330, 140, { r: 0, g: 0, b: 0, a: 200 });
    text("ORIENTATION", W - 330, 30, 18, COL.GRAY);

    const fwd = getForward(ship.rot);
    const pitch = Math.atan2(fwd.y, Math.sqrt(fwd.x * fwd.x + fwd.z * fwd.z)) * 180 / Math.PI;
    text(`Pitch: ${pitch.toFixed(1)}°`, W - 330, 55, 20, COL.WHITE);

    const right = getRight(ship.rot);
    const upVec = getUp(ship.rot);
    const roll = Math.atan2(right.y, upVec.y) * 180 / Math.PI;
    text(`Roll: ${roll.toFixed(1)}°`, W - 330, 85, 20, COL.WHITE);

    const vSpeed = ship.vel.y * 10;
    const vSpeedStr = vSpeed > 0 ? `↑${vSpeed.toFixed(1)}` : `↓${Math.abs(vSpeed).toFixed(1)}`;
    const vSpeedCol = vSpeed > 0 ? COL.GREEN : COL.ORANGE;
    text(`V/S: ${vSpeedStr} m/s`, W - 330, 115, 20, vSpeedCol);

    text("Hako + Raylib + TypeScript", W / 2 - 140, H - 30, 18, COL.YELLOW);

    endDraw();
}

while (!shouldClose()) {
    time += 0.016;

    if (state === STATE_MENU) {
        menu();
        if (pendingStart) await startGame();
    } else if (state === STATE_PLAYING) {
        const dt = 1 / 60;

        updateShip(dt);
        updateCamera(dt);
        updateDust();

        if (Math.floor(time * 2) % 30 === 0) {
            loadChunks();
            buildCache();
        }

        updateSound();
        renderGame();
    }
}

close();
shutdownAudio();