# Scrabe for information

Plang provides access to a [browser module](https://github.com/PLangHQ/plang/blob/main/Documentation/modules/PLang.Modules.WebCrawlerModule.md), this make is possible to retrieve information easily when you don't have database access.

In our [Simple decisions](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/TheSimpleDecisions.md) article, we are retrieving data from database, but we don't always have access to the database, but we might have access to the website or even the admin system that answer the question we are looking for.

In the [Simple decisions](https://github.com/PLangHQ/plang/blob/main/Documentation/blogs/TheSimpleDecisions.md) article, we retrieve a coupon from a database, but let's imagine we dont have access to the database, but we do have access to the admin system.

So let's crawl the admin system for the data.

## Plan

We need to understand how the admin url paths work and where to find the information. We need to login to the system. So this is the plan.

> Note: This is a made up scenario

- login to the admin system, located at https://example.org/admin
- go to the search user by email url, located at https://example.org/admin/users/?email=%email%
- find the user id 
- go to https://example.org/admin/users/%userId%
- parse the orders listed on the user page, this include coupon information
- ask LLM to get the coupon from list of orders
- answer user

## Code

We have three variables available to us that is sent into the plang app:
- %email% of user
- %body% is the email body
- %subject% is the email subject

Let's code this in plang

```plang
- open https://example.org/admin
- set #username = %Settings.Username%
- set #password = %Settings.Password%
- click input[type=submit]
- navigate to https://example.org/admin/users/?email=%email%
- extract the text from '.users .id', write to %userId%
- navigate to https://example.org/admin/users/%userId%
- extract text from '.orders', write to %orders%
/ The rest is now same the the Simple decision article, after we the database call
```

We have now logged into the admin system, scrabed the information we needed, and can let the LLM make the decision for us.


