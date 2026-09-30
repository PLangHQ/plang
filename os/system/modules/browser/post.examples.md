Step text: `post %!data% to the page of %browser%`
Properties: `{"Data": "%!data%", "Browser": "%browser%"}` — browser.post, not browser.send: send gives the browser input (mouse, keys), post gives the page a message. Not browser.callGoal: post gives a message, callGoal calls one of the page's goals by name.
