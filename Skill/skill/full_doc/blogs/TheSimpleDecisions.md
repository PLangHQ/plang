# The Simple Decisions

Every day, we make simple decisions. These decisions don't require much brainpower, but they do require effort. You have to open that email, log into the system, look something up, and say, "that's fine."

While these decisions may not seem critical to us, they can be essential for others, and they fill up our day-to-day lives.

Now, imagine if we could offload many of these mundane tasks to a computer. Since computers are getting better at understanding our intentions, they could handle the lookups and make those straightforward decisions for us.

This is where Plang shines.

### A Customer Support Example

Picture this: You're working in customer support. You receive a flood of emails, and most of them revolve around the same 10 issues. 

I have a client whose customer support tickets are 90% the same recurring problems. So, what do we do? We create a goal for each of those issues.

Take one common issue: a user has lost their coupon purchased from the website. 

The email looks something like this:

```
From: user@example.org  
Hi, I lost my coupon for the Christmas show. Can you help me?
```

Let's solve this with Plang.

## The Plan

The system gets the email, and now it has to make a decision:

1. Can we process this email?
2. If we can, find the goal that matches the email's content.
3. If we can't, do nothing.

### Code Breakdown

We have three variables available that are sent into the Plang app:
- `%email%`: the user's email
- `%body%`: the email's content
- `%subject%`: the email's subject line

Let’s create a goal called `ProcessEmail.goal`:

```plang
ProcessEmail
- [llm] system: You are processing an email.
            Your task is to select the goal that matches the email. 
            If no goal is found, set the goal to null.
            == Goals ==
            GoalName: LostCoupon
            Description: When a user is asking about their lost coupons.
            == Goals ==
        user: "Subject: %subject%
                Body: %body%"
        scheme: {GoalName: string}
- if %GoalName% is not null, call goal %GoalName%
```

Next, we create the file `LostCoupon.goal`. 

We now need to query the database to find the user and retrieve the products they’ve purchased. We do this by looking up the user in the database via their email, `user@example.org`, and fetching all associated orders.

```plang
LostCoupon
- select u.name as userName, p.name as productName, o.coupon_number 
        from users u
        join orders o on u.id=o.user_id
        join products p on p.id=o.product_id
        where u.email=%email%
    write to %orders%
- [llm] system: You have a list of the user’s orders.
    Look for the product and coupon related to the user's request.
    If you find the matching coupon, create an email to the user using their name.
    == orders ==
    %orders%
    == orders ==
    user: %body%
    scheme: {isFound: bool, body: string}
- if %isFound% then call CouponFoundEmail, else call NotFoundCouponEmail

CouponFoundEmail
- send email to %email%, %body%

NotFoundCouponEmail
- send email to %email%, body="We could not find your coupon"
```

> Note: This is a simplified version for the sake of the article. In a real scenario, you'd need to add more detail for the LLM and also ensure the email is marked as processed in your system.

We define the `scheme` as `{isFound: bool, body: string}`, and the LLM will use this information to proceed.

## No Database Access?

Let’s say you don’t have access to a database but instead have access to an admin system that can provide the information you need. This can be solved with the WebCrawler module.

```plang
CrawlForCoupon
- open https://example.org/admin
- set #username = %Settings.Username%
- set #password = %Settings.Password%
- click #login
- set #email input value %email%
- click #lookupUser button
- extract #coupons, to %coupons%
```

Now, you’ve collected all the user’s coupons into a variable. You can send this data to the LLM by calling the `LostCoupon` goal, or if you want to cut down on LLM costs, you could parse the HTML yourself.

This simple flow shows how Plang can automate the small, everyday decisions that tend to pile up, leaving you more time to focus on the work that really matters.