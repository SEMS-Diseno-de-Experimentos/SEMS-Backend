const jwt = require("jsonwebtoken");
const token = jwt.sign({ sub: "00000000-0000-0000-0000-000000000000" }, "replace_with_minimum_32_chars_secret_key_for_hs256");
console.log(token);
