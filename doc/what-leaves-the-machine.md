# What leaves the machine

For someone deciding whether to trust PLang. The question this page answers: what does PLang send over the network, to whom, and what is in it?

The endpoints can be set to a local server, which is how this page was checked: `TYPESAFE_ENDPOINT` for the build's service and `OPENAI_API_ENDPOINT` for the model. A local server that logged each request (the body, and whether an `Authorization` header was present) was used. Key values were not read.

## A run of a built goal

A goal that was already built, with no model or http step, made no request. A run of a goal that reads a file made 0 requests to either endpoint. Running needs no network.

## A build

`plang build` sends one request per goal that has steps not yet built, to the decider service (`https://api.typesafe.ai/v1/systemone` by default). The request:

- is a POST with `Authorization`, `Content-Type`, `Content-Length` and `Host` headers;
- has a JSON body with the keys `model`, `questions` and `state`;
- carries the text of the goal's steps (a literal in a step, `hunter2-literal`, was in the body);
- carries the catalog of modules and actions (about 37 KB of instructions in the one request seen);
- did not carry the contents of a file the goal reads (a file holding `SECRETCONTENT123` read by a step did not appear in the body);
- did not carry the app's folder path.

A rebuild with nothing changed builds no goal. A request count was not taken for it.

## A model step

A step such as `ask the model "…"` builds to `llm.query`. At run it sends a POST to the model endpoint (`https://api.openai.com/v1/chat/completions` by default) with `Authorization`, and a body with `model`, `messages`, `temperature` and `max_completion_tokens`.

What the `messages` hold: the step's text with each variable replaced by its value, so `ask the model "Say hello to %name%"` sends the value of `%name%` and the value is what leaves the machine. A variable that does not exist fails the step instead of being sent as written. A value that itself contains `%secret%` is sent as that text, never read again. Read at `PLang/app/module/llm/query.cs:62-83` and `code/OpenAi.cs:70-74`; run as C# tests, not as a live request: `RequiredVariableTests` (4 of 4 pass) and `MessageRenderProbeTests` (2 of 2 pass).
The file a goal read earlier in the same run (`FILECONTENT456`) was not in the request.

## http

An `http` step sends what the step names: its URL, its headers, its body, to that URL. In the runs for [http](http.md), the local echo server reported the method, the `Authorization` header the step set and the body. It did not report other headers, so what else the request carried was not looked at here.

## Anything else

No other request was seen in the runs behind these pages. That is not a proof that none exists: only the runs described here were looked at. A packet capture was not taken.

## Tests

No test was found for "a run makes no request" or for "a build does not send file contents". `DeciderProviderTests` exists in `PLang.Tests/Modules/App/Modules/llm/`; it was not read for this page.
