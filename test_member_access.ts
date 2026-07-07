// Test file for member property access translation
function testLength() {
    const items = ["a", "b", "c"];
    for (let i = 0; i < items.length; i++) {
        console.log(items[i]);
    }
}

function testName() {
    const obj = { name: "test" };
    console.log(obj.name);
}

function testMessage() {
    const error = { message: "error occurred" };
    console.log(error.message);
}

function testValue() {
    const input = { value: "input text" };
    console.log(input.value);
}
