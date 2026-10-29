Step text: `post %event% to %browser.desktop%`
Formal: `window.post(Data: item = %event%, Window: item = %browser.desktop%)` — the desktop is the browser's first window. window.post, not browser.send: send gives the browser input (mouse, keys), post gives a page a message. Not window.call: post gives a message, call calls one of the page's goals by name.

Step text: `post %status% to window %asked.from% of %browser%`
Formal: `window.post(Data: item = %status%, Window: item = %asked.from%, Browser: browser = %browser%)`
