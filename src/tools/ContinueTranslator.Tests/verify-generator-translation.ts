// Test TypeScript file to validate async generator translation
export async function* recursiveStream(
  llm: object,
  abortController: object,
  type: object,
  prompt: object,
  prediction: object,
  currentBuffer: string = "",
  isContinuation: boolean = false
): object {
  yield "test";
}

export async function* simpleAsyncGen(): object {
  yield 1;
  yield 2;
  yield 3;
}

export function* regularGen(): object {
  yield "a";
  yield "b";
}

export async function normalAsync(): object {
  return "hello";
}
