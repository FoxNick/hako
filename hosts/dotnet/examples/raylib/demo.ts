import {
    initWindow,
    closeWindow,
    windowShouldClose,
    setTargetFPS,
    getScreenWidth,
    getScreenHeight,
    beginDrawing,
    endDrawing,
    clearBackground,
    drawText,
    drawFPS,
    isKeyPressed,
    Vector2,
    Vector3,
    Camera3D,
    Color,
    beginMode3D,
    endMode3D,
    drawCube,
    drawCubeWires,
    drawPlane,
    colorAlpha,
    KEY_SPACE,
    KEY_ONE,
    KEY_TWO,
    KEY_THREE,
    KEY_FOUR,
    CAMERA_PERSPECTIVE,
    RAYWHITE,
    BLUE,
    SKYBLUE,
    DARKPURPLE,
    PURPLE,
    GOLD,
    LIME,
    MAROON,
    WHITE,
    LIGHTGRAY,
    BLACK
} from 'raylib';

const screenWidth = 1280;
const screenHeight = 720;

initWindow(screenWidth, screenHeight, "hako + raylib");

const camera = new Camera3D();
camera.position = new Vector3(25.0, 15.0, 25.0);
camera.target = new Vector3(0.0, 0.0, 0.0);
camera.up = new Vector3(0.0, 1.0, 0.0);
camera.fovy = 60.0;
camera.projection = CAMERA_PERSPECTIVE;


interface SceneObject {
    pos: Vector3;
    type: 'cube' | 'tower' | 'ring';
    offset: number;
}

const objects: SceneObject[] = [];


const gridSize = 5;
for (let x = -gridSize; x <= gridSize; x++) {
    for (let z = -gridSize; z <= gridSize; z++) {
        const dist = Math.sqrt(x * x + z * z);
        let type: 'cube' | 'tower' | 'ring' = 'cube';

        if (dist < 2) {
            type = 'tower';
        } else if (dist > 6 && Math.abs(x) % 2 === 0) {
            type = 'ring';
        }

        objects.push({
            pos: new Vector3(x * 3.5, 0, z * 3.5),
            type: type,
            offset: (x + z) * 0.5 + dist * 0.3
        });
    }
}

let time = 0.0;
let paused = false;
let scene = 1;

setTargetFPS(60);

console.log(`Enhanced demo initialized with ${objects.length} objects`);


function lerpColor(c1: Color, c2: Color, t: number): Color {
    return new Color(
        Math.floor(c1.r + (c2.r - c1.r) * t),
        Math.floor(c1.g + (c2.g - c1.g) * t),
        Math.floor(c1.b + (c2.b - c1.b) * t),
        255
    );
}


while (!windowShouldClose()) {
    if (isKeyPressed(KEY_SPACE)) {
        paused = !paused;
    }
    if (isKeyPressed(KEY_ONE)) scene = 1;
    if (isKeyPressed(KEY_TWO)) scene = 2;
    if (isKeyPressed(KEY_THREE)) scene = 3;
    if (isKeyPressed(KEY_FOUR)) scene = 4;

    if (!paused) {
        time += 0.016;
    }


    const radius = scene === 3 ? 40 : 35;
    const height = scene === 4 ? 20 + Math.sin(time * 0.3) * 5 : 18;
    const angle = time * 0.3;

    camera.position = new Vector3(
        Math.cos(angle) * radius,
        height,
        Math.sin(angle) * radius
    );
    camera.target = new Vector3(0, scene === 2 ? 5 : 3, 0);

    beginDrawing();


    const bgColor = scene === 4
        ? new Color(25, 20, 45, 255)
        : new Color(30, 35, 50, 255);
    clearBackground(bgColor);

    beginMode3D(camera);


    drawPlane(new Vector3(0, -0.5, 0), new Vector2(100, 100), new Color(20, 25, 35, 255));


    for (let i = -15; i <= 15; i += 3) {
        drawCube(new Vector3(i, -0.4, 0), 0.1, 0.1, 50, new Color(40, 50, 70, 100));
        drawCube(new Vector3(0, -0.4, i), 50, 0.1, 0.1, new Color(40, 50, 70, 100));
    }


    for (let i = 0; i < objects.length; i++) {
        const obj = objects[i];
        const dist = Math.sqrt(obj.pos.x * obj.pos.x + obj.pos.z * obj.pos.z);

        let height: number;
        let color: Color;
        let wireColor: Color;

        switch (scene) {
            case 1:
                height = 2 + Math.sin(time * 2 - dist * 0.4 + obj.offset) * 2;
                const t1 = (Math.sin(time * 2 - dist * 0.4 + obj.offset) + 1) * 0.5;
                color = lerpColor(BLUE, PURPLE, t1);
                wireColor = colorAlpha(SKYBLUE, 0.5);
                break;

            case 2:
                height = 2 + Math.sin(time * 3 - dist * 0.6) * 2.5;
                const t2 = (dist / 20);
                color = lerpColor(PURPLE, DARKPURPLE, t2);
                wireColor = colorAlpha(WHITE, 0.3);
                break;

            case 3:
                const spiralAngle = Math.atan2(obj.pos.z, obj.pos.x);
                height = 2 + Math.sin(time * 2 + spiralAngle * 3 - dist * 0.3) * 2;
                const t3 = (Math.sin(spiralAngle * 2 + time) + 1) * 0.5;
                color = lerpColor(PURPLE, GOLD, t3);
                wireColor = colorAlpha(LIME, 0.4);
                break;

            case 4:
                height = 2 + Math.sin(time * 4 - dist * 0.2 + obj.offset * 2) * 1.5;
                const t4 = (Math.sin(time * 2 + obj.offset) + 1) * 0.5;
                color = lerpColor(MAROON, GOLD, t4);
                wireColor = colorAlpha(RAYWHITE, 0.6);
                break;

            default:
                height = 2;
                color = BLUE;
                wireColor = SKYBLUE;
        }


        if (obj.type === 'tower') {
            height *= 1.5;
            drawCube(
                new Vector3(obj.pos.x, height / 2, obj.pos.z),
                1.2, height, 1.2,
                color
            );
            drawCubeWires(
                new Vector3(obj.pos.x, height / 2, obj.pos.z),
                1.2, height, 1.2,
                wireColor
            );
        } else if (obj.type === 'ring') {
            drawCubeWires(
                new Vector3(obj.pos.x, height / 2, obj.pos.z),
                2, height, 2,
                color
            );
        } else {
            drawCube(
                new Vector3(obj.pos.x, height / 2, obj.pos.z),
                1.5, height, 1.5,
                color
            );
            drawCubeWires(
                new Vector3(obj.pos.x, height / 2, obj.pos.z),
                1.5, height, 1.5,
                wireColor
            );
        }
    }


    const centerHeight = 6 + Math.sin(time * 1.5) * 2;
    const centerPulse = Math.abs(Math.sin(time * 2));


    drawCube(
        new Vector3(0, centerHeight / 2, 0),
        2.5, centerHeight, 2.5,
        scene === 4 ? GOLD : DARKPURPLE
    );
    drawCubeWires(
        new Vector3(0, centerHeight / 2, 0),
        2.5, centerHeight, 2.5,
        colorAlpha(WHITE, 0.7)
    );


    const ringHeight = centerHeight + 2;
    const ringRotation = time * 2;
    for (let i = 0; i < 4; i++) {
        const angle = ringRotation + (i * Math.PI / 2);
        const x = Math.cos(angle) * 4;
        const z = Math.sin(angle) * 4;
        drawCube(
            new Vector3(x, ringHeight, z),
            0.8, 0.8, 0.8,
            lerpColor(SKYBLUE, LIME, centerPulse)
        );
    }


    for (let i = 0; i < 8; i++) {
        const satAngle = time * 1.5 + (i * Math.PI / 4);
        const satRadius = 8;
        const x = Math.cos(satAngle) * satRadius;
        const z = Math.sin(satAngle) * satRadius;
        const y = 5 + Math.sin(time * 3 + i) * 1.5;

        drawCube(
            new Vector3(x, y, z),
            0.6, 0.6, 0.6,
            colorAlpha(GOLD, 0.8)
        );
    }

    endMode3D();


    drawFPS(10, 10);
    drawText(`Scene: ${scene}/4`, 10, 40, 20, RAYWHITE);
    drawText(`Objects: ${objects.length}`, 10, 65, 20, RAYWHITE);


    const controlsY = getScreenHeight() - 90;
    drawText("Controls:", 10, controlsY, 18, LIGHTGRAY);
    drawText("Space: Pause  |  1-4: Change Scene", 10, controlsY + 25, 16, RAYWHITE);


    const sceneName = ["Wave", "Ripple", "Spiral", "Pulse"][scene - 1];
    const nameWidth = sceneName.length * 15;
    drawText(sceneName, getScreenWidth() / 2 - nameWidth / 2, 20, 30,
        colorAlpha(GOLD, 0.8));

    if (paused) {
        const pauseText = "PAUSED";
        drawText(pauseText, getScreenWidth() / 2 - 70, getScreenHeight() / 2 - 20, 40,
            colorAlpha(WHITE, 0.9));
    }

    endDrawing();
}

closeWindow();
console.log("Enhanced demo closed");
