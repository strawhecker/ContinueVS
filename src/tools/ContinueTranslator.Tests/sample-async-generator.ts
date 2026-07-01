// Sample TypeScript file with async generator for testing translator
export const generatorWithCancellation = async function* () {
  for await (const update of someAsyncIterable) {
    if (token.aborted) {
      return;
    }
    yield update;
  }
};

// Simple async generator
export async function* simpleAsyncGen() {
  yield 1;
  yield 2;
  yield 3;
}

// Regular generator (non-async)
export function* regularGen() {
  yield "a";
  yield "b";
}
