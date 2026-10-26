# HTTP

For someone writing a program. The question this page answers: how do I call a web address and use the answer?

## Get

```plang
- get http://127.0.0.1:8765/weather.json, write to %weather%
- write out "city: %weather.city%"
```

Output: `city: Reykjavik`. The server returned `{"city":"Reykjavik","temp":4}`; a JSON answer is read into a value you can navigate with a dot, as `%weather.city%`.

## Post with headers and a body

```plang
- post http://127.0.0.1:8766/echo, headers Authorization: "Bearer abc", body {"x": 1}, write to %reply%
- write out "method: %reply.method%, auth: %reply.auth%"
- write out "body: %reply.body%"
```

Output against a small test server that answers a JSON object with the fields `method`, `auth` (the `Authorization` header it received) and `body` (the text it received):

```
method: POST, auth: Bearer abc
body: {"x":1}
```

Those fields belong to that test server, not to PLang. `%reply%` holds whatever the server answers. To see its shape, print it: `write out %reply%`. Then read the fields that exist, with a dot.

GET is the default verb. A JSON body is the request's body. A header of another name (`X-Token: "abc"`) was sent in a cold-reader run, but the test server only reported `Authorization`, so that run could not show the header arriving.

## Download a file

A step that says `download` or saves a URL to a path is `http.download`, a different action. See `os/system/modules/http/download.description.md`. Not run here.

## Timeouts

`get %url%, timeout after 10 seconds` gives the request a deadline. See [timeouts](timeouts.md).

The `request` action: `os/system/modules/http/request.description.md` and `request.examples.md`.

Both servers were local ones started for the check. A request to a public address was not tried.
