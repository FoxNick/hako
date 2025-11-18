
import { V3 } from "raylib";

declare module 'math' {
  export interface Quat {
    readonly x: number;
    readonly y: number;
    readonly z: number;
    readonly w: number;
  }

  export function quatIdentity(): Quat;
  export function quatFromEuler(pitch: number, yaw: number, roll: number): Quat;
  export function quatFromAxisAngle(axis: V3, angle: number): Quat;
  export function quatMultiply(q1: Quat, q2: Quat): Quat;
  export function quatRotateVector(v: V3, q: Quat): V3;
  export function quatSlerp(q1: Quat, q2: Quat, t: number): Quat;
  export function smoothDampFloat(from: number, to: number, speed: number, dt: number): number;
  export function smoothDampV3(from: V3, to: V3, speed: number, dt: number): V3;
  export function smoothDampQuat(from: Quat, to: Quat, speed: number, dt: number): Quat;
  export function v3Add(a: V3, b: V3): V3;
  export function v3Scale(v: V3, s: number): V3;
  export function v3Length(v: V3): number;
  export function v3Distance(a: V3, b: V3): number;
  export function v3Normalize(v: V3): V3;
  export function clamp(value: number, min: number, max: number): number;
  export function degToRad(deg: number): number;
}
