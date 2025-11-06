
declare module 'raylib' {
  export class Vector2 {
    constructor(x?: number, y?: number);
    x: number;
    y: number;
  }

  export class Vector3 {
    constructor(x?: number, y?: number, z?: number);
    x: number;
    y: number;
    z: number;
  }

  export class Camera3D {
    constructor();
    position: Vector3;
    target: Vector3;
    up: Vector3;
    fovy: number;
    projection: number;
  }

  export class Color {
    constructor(r?: number, g?: number, b?: number, a?: number);
    r: number;
    g: number;
    b: number;
    a: number;
  }

  export const KEY_SPACE: number;
  export const KEY_ONE: number;
  export const KEY_TWO: number;
  export const KEY_THREE: number;
  export const KEY_FOUR: number;
  export const KEY_P: number;
  export const KEY_W: number;
  export const KEY_A: number;
  export const KEY_S: number;
  export const KEY_D: number;
  export const KEY_UP: number;
  export const KEY_DOWN: number;
  export const KEY_LEFT: number;
  export const KEY_RIGHT: number;
  export const CAMERA_CUSTOM: number;
  export const CAMERA_FREE: number;
  export const CAMERA_FIRST_PERSON: number;
  export const CAMERA_THIRD_PERSON: number;
  export const CAMERA_ORBITAL: number;
  export const CAMERA_PERSPECTIVE: number;
  export const CAMERA_ORTHOGRAPHIC: number;
  export const WHITE: Color;
  export const RAYWHITE: Color;
  export const LIGHTGRAY: Color;
  export const GRAY: Color;
  export const DARKGRAY: Color;
  export const MAROON: Color;
  export const BLUE: Color;
  export const SKYBLUE: Color;
  export const LIME: Color;
  export const GOLD: Color;
  export const PURPLE: Color;
  export const DARKPURPLE: Color;
  export const BLACK: Color;

  export function initWindow(width: number, height: number, title: string): void;
  export function closeWindow(): void;
  export function windowShouldClose(): boolean;
  export function setTargetFPS(fps: number): void;
  export function getScreenWidth(): number;
  export function getScreenHeight(): number;
  export function beginDrawing(): void;
  export function endDrawing(): void;
  export function clearBackground(color: Color): void;
  export function drawCircleV(center: Vector2, radius: number, color: Color): void;
  export function drawText(text: string, x: number, y: number, fontSize: number, color: Color): void;
  export function drawFPS(x: number, y: number): void;
  export function drawRectangle(x: number, y: number, width: number, height: number, color: Color): void;
  export function drawRectangleLines(x: number, y: number, width: number, height: number, color: Color): void;
  export function updateCamera(camera: Camera3D, cameraMode: number): void;
  export function beginMode3D(camera: Camera3D): void;
  export function endMode3D(): void;
  export function drawPlane(centerPos: Vector3, size: Vector2, color: Color): void;
  export function drawCube(position: Vector3, width: number, height: number, length: number, color: Color): void;
  export function drawCubeWires(position: Vector3, width: number, height: number, length: number, color: Color): void;
  export function getRandomValue(min: number, max: number): number;
  export function colorAlpha(color: Color, alpha: number): Color;
  export function fade(color: Color, alpha: number): Color;
  export function isKeyPressed(key: number): boolean;
  export function isKeyDown(key: number): boolean;
  export function isKeyPressedRepeat(key: number): boolean;
  export function isKeyReleased(key: number): boolean;
  export function isKeyUp(key: number): boolean;
}
