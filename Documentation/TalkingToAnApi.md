# Talking to an api

Everything on this page came out of wiring one plang app to a payment provider, a laboratory api
and a mail service. The recurring theme: the parts that break are never the http call, they are the
shape of somebody else's data and the mock you thought was protecting you.

## A key with a dash in it cannot be read from a variable path

Webhook payloads are full of them. Mailgun posts `stripped-text`, `body-plain`, `Message-Id`,
`In-Reply-To`. None of these work:

```plang
- set %text% = %request.body.stripped-text%        # reads it as body.body minus plain
- set %text% = %request.body["stripped-text"]%     # looks for a variable of that name
- set %key% = "stripped-text"
- set %text% = %request.body[key]%                 # index has to be a number
```

Use the dictionary module:

```plang
- [list] get value of key "stripped-text" from dictionary %request.body%, write to %text%
```

The same goes for any key with a character the path parser treats as an operator.

## An object is not a sql parameter

```plang
- update payments set gatewayResponse=%status% where id=%id%     # %status% is an object
```

fails at runtime. Serialise first, or store the fields you actually need in their own columns. Note
that `%something.ToJson()%` works on a list but **not** on a dictionary, so a posted body cannot be
turned into a string that way. If you need the raw payload, write it with the file module instead.

## Say which datasource on every step that leaves the default

An app with several databases resolves the datasource at build time and bakes it into the `.pr`.
When a goal touches more than one, be explicit on each step:

```plang
- select id as %userId% from users where email=%email%
    ds: "data"
    return 1 row
- insert into messages, ..., ds: "mail"
```

Silence here does not mean the default, it means whatever the builder decided, and a step that was
built while a different datasource was current keeps that decision until the `.pr` is regenerated.

## Form posts, multipart, and files

`post` sends json. For a form encoded api, say so:

```plang
- post https://api.example.com/messages
    headers {"Authorization": "Basic %Settings.ApiAuth%"}
    content type "application/x-www-form-urlencoded"
    body %data%
```

For files, `post multipart form data` takes a value prefixed with `@`, which is either a path or a
base64 string, and may carry `;type=` and `;name=`:

```plang
- set %data% = {"from": "%sender%", "to": "%to%", "attachment": %files%}
- [http] post multipart form data to https://api.example.com/messages
    data %data%
    headers {"Authorization": "Basic %Settings.ApiAuth%"}
```

where `%files%` is a list like `["@<base64>;type=image/png;name=photo.png"]`. A list under one field
name becomes several parts with that name, which is what apis that accept repeated `attachment`
fields expect.

Basic auth needs the base64 computed in advance and stored as a setting. Nesting a variable inside a
string inside a variable, `%"api:%Settings.Key%".ToBase64()%`, does not build.

## A webhook has no identity

The service posting to you is not a browser and carries no identity, so the route cannot be guarded
the usual way. Authenticate the caller explicitly, either with the signature the service offers or
with a secret in the url that you check first:

```plang
Inbound
- if %request.query.secret% == %Settings.InboundSecret% then call goal Store, else call goal Reject
```

Make the handler idempotent while you are there. Services retry when they do not get a 200, so a
unique external id and a check for it is the difference between a thread and forty copies of it.
See [Identity in the browser](./IdentityInTheBrowser.md) for why the guard cannot help you here.

## The mock only matches the method the step built as

This is the one that costs money. A mock is bound to a module **and a method name**:

```plang
- mock http post url:https://api.example.com/messages, call MockSend
```

`post` binds `Post`. The moment the step changes to `post multipart form data` the method is
`PostMultipartFormData`, the mock stops matching, and the development app talks to the live service
with no warning at all. I found out because a message id in my database was in the provider's
format instead of my mock's.

Two rules follow:

- **Change the step, check the mock.** They are coupled by a name that nothing validates.
- **Verify the mock fired**, not that the page looked right. Give the mock a recognisable value and
  assert on it.

Wildcards in the url match at the start or the end only. A `*` in the middle,
`https://api.example.com/v3/*/messages`, never matches, and again fails open, straight to the real
service.

## Keep the vendor behind a boundary

One folder knows the vendor's urls, field names and ids, and nothing else does. External ids live in
columns named for it, `externalAppointmentId`, never in the primary key. When the second supplier
arrives, or the first one changes a field name, the change is in one folder. This is a convention
rather than a language feature, but it is the one that has paid for itself most often.

## See also

- [Writing goals that behave](./WritingGoals.md)
- [Building plang code](./Building.md)
- [Module reference](./modules/reference/README.md), for the exact method and its parameters.
