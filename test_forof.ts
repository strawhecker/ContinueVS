// Test file for ForOf destructuring
function testForOf() {
  const data = new Map([['a', ['x', 'y']], ['b', ['z']]]);
  for (const [key, values] of data.entries()) {
    console.log(key, values.join(','));
  }
}
