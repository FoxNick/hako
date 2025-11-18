
declare module 'raylib' {
  export interface V3 {
    x: number;
    y: number;
    z: number;
  }

  export interface Cam {
    pos: V3;
    tar: V3;
    up: V3;
    fov: number;
  }

  export interface Col {
    r: number;
    g: number;
    b: number;
    a: number;
  }

  export const KEY_W: number;
  export const KEY_A: number;
  export const KEY_S: number;
  export const KEY_D: number;
  export const KEY_SPACE: number;
  export const KEY_UP: number;
  export const KEY_DOWN: number;
  export const KEY_LEFT: number;
  export const KEY_RIGHT: number;
  export const KEY_ENTER: number;
  export const KEY_Q: number;
  export const KEY_E: number;

  export function init(w: number, h: number, t: string): void;
  export function close(): void;
  export function shouldClose(): boolean;
  export function setFPS(fps: number): void;
  export function beginDraw(): void;
  export function endDraw(): void;
  export function clear(c: Col): void;
  export function text(t: string, x: number, y: number, s: number, c: Col): void;
  export function rect(x: number, y: number, w: number, h: number, c: Col): void;
  export function begin3D(cam: Cam): void;
  export function end3D(): void;
  export function cube(p: V3, w: number, h: number, l: number, c: Col): void;
  export function isKeyDown(k: number): boolean;
  export function isKeyPressed(k: number): boolean;
}
