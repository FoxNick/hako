
declare module 'audio' {
  export function init(): void;
  export function play(freq: number, ms: number, vol: number): void;
  export function stop(): void;
  export function shutdown(): void;
}
