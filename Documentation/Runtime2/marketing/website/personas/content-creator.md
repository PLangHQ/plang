# PLang for Content Creators

---

## Headline
**Publish everywhere from one workflow.**

---

## The Daily Grind

You create one piece of content, then spend an hour distributing it. Upload to the blog. Cross-post to Medium. Schedule the social posts. Update the content calendar spreadsheet. Send the newsletter. Check analytics from last week's post to inform this week's topic.

The creating part takes 2 hours. The publishing and tracking part takes another 2. And half the time you forget one platform.

---

## The PLang Way

```plang
Start
- start webserver
- every day at 8am, call !CheckContentCalendar

PublishPost - POST
- insert into posts, title=%request.title%, body=%request.body%, status='published', publishedAt=%Now%, write to %postId%
- call !DistributePost
- write out {postId: %postId%, status: 'published and distributed'}

DistributePost
- post https://api.medium.com/v1/users/me/posts
    data={title: '%request.title%', content: '%request.body%', contentFormat: 'markdown', publishStatus: 'public'}
    write to %mediumResult%
- post https://api.twitter.com/2/tweets
    data={text: 'New post: %request.title% - Read more at https://myblog.com/posts/%postId%'}
    write to %tweetResult%
- insert into distribution_log, postId=%postId%, medium=%mediumResult.id%, twitter=%tweetResult.id%, date=%Now%
- write out 'Distributed to Medium and Twitter'

CheckContentCalendar
- select * from content_calendar where dueDate=%Tomorrow% and status='planned', write to %dueTomorrow%
- foreach %dueTomorrow%, call !RemindToWrite item=%item%

RemindToWrite
- send email to me@creator.com, subject: "Content Due Tomorrow: %item.title%", body: "Your planned content '%item.title%' is due tomorrow. Topic notes: %item.notes%"

WeeklyAnalytics
- select title, publishedAt from posts where publishedAt >= %Now-7days%, write to %recentPosts%
- select count(*) as totalPosts from posts, write to %total%
- send email to me@creator.com, subject: "Weekly Content Report", body: "Posts this week: %recentPosts.count%\nAll-time total: %total.totalPosts%\n\nRecent:\n%recentPosts%"

AddToCalendar - POST
- insert into content_calendar, title=%request.title%, notes=%request.notes%, dueDate=%request.dueDate%, status='planned'
- write out {status: 'added to calendar'}
```

---

## Wait — that's the program?

That's your publishing workflow. Publish once, distribute to multiple platforms, track in a content calendar, get daily reminders for upcoming content, weekly analytics summary. One file.

---

## What Just Happened

- **`post https://api.medium.com/...`** — Cross-post to Medium automatically.
- **`post https://api.twitter.com/...`** — Tweet the link automatically.
- **`insert into distribution_log`** — Track where each post was distributed.
- **`every day at 8am`** — Content calendar checked daily. Reminders sent for tomorrow's deadlines.
- **`insert into content_calendar`** — Your content calendar lives in a database, not a spreadsheet.
- **`send email`** — Reminders and reports delivered to your inbox.

Want to add LinkedIn? Add one more `post https://api.linkedin.com/...` line. Want to change the reminder window to 2 days? Adjust the query. Your publishing workflow adapts as your distribution grows.

---

## The Build / Run Split

PLang uses AI at **build time** to compile your English into executable instructions. At **runtime**, no AI — your publishing workflow runs reliably with zero ongoing cost. Build once, your content distribution runs itself.

---

## Get Started

```bash
# Download from https://github.com/PLangHQ/plang/releases
mkdir ContentTools && cd ContentTools
# Create Start.goal with your publishing workflow
plang exec
```

Write your publishing process in plain English. Create more, distribute less.

[Full getting started guide →](/get-started)
