# Lesson 3: Let's Create an App

Let's create an app that downloads user information from a URL and writes it into a database.

We will be using:
```curl
https://jsonplaceholder.typicode.com/users/1
```

It gives us this JSON:
```json
{
  "id": 1,
  "name": "Leanne Graham",
  "username": "Bret",
  "email": "Sincere@april.biz",
  "address": {
    "street": "Kulas Light",
    "suite": "Apt. 556",
    "city": "Gwenborough",
    "zipcode": "92998-3874",
    "geo": {
      "lat": "-37.3159",
      "lng": "81.1496"
    }
  },
  "phone": "1-770-736-8031 x56442",
  "website": "hildegard.org",
  "company": {
    "name": "Romaguera-Crona",
    "catchPhrase": "Multi-layered client-server neural-net",
    "bs": "harness real-time e-markets"
  }
}
```

We want to store the name, email, and address.

To store something, we need a database. Luckily, Plang has a built-in database. So let's define that table.

We create `Setup.goal` file and give the goal name `Setup`:

```plang
Setup
- create table users, columns: name, email, address(json), created(datetime, now)
```

I also added the created date; always nice to have.

Next, we want to create the `Start.goal` file and give it the goal name `Start`:

```plang
Start
- get https://jsonplaceholder.typicode.com/users/1, into %userInfo%
- insert into users, %userInfo.name%, %userInfo.email%, %userInfo.address%
- select * from users, write to %users%
- write out %users%
```

Open the console/terminal, go to the working directory, and run:
```bash
plang build
```

It will build the `Setup.goal` file, and then it will fail.

It fails because the tables haven't been created in the database, and we need them to validate the `insert` and `select` queries.

So now you have to run:
```bash
plang run setup
```

The `Setup` file will run and create the table in the database.

Now you can run:
```bash
plang build
```

After you have built it, run:
```bash
plang
```

You should now see one user; refresh, and they will become two.

Next is [Lesson 4 - How Does It Really Work?](./Lesson%204.md).