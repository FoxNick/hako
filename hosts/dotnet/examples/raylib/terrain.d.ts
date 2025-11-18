
declare module 'terrain' {
  export interface Chunk {
    readonly chunkX: number;
    readonly chunkZ: number;
    readonly blocks: readonly Block[];
  }

  export interface Block {
    readonly x: number;
    readonly y: number;
    readonly z: number;
    readonly r: number;
    readonly g: number;
    readonly b: number;
  }

  export function setSeed(seed: number): void;
  export function preloadAsync(cx: number, cz: number, radius: number, size: number, bs: number): Promise<void>;
  export function getChunk(cx: number, cz: number, size: number, bs: number): Chunk;
  export function getHeight(x: number, z: number): number;
  export function clear(): void;
}
