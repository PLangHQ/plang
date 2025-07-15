# mvp.css
Out of the box CSS styling for HTML elements. No class names, no framework to learn.

To include in html:
- mvp.css

Documentation: https://andybrewer.github.io/mvp/



/* default style */
:root {
    --active-brightness: 0.85;
    --border-radius: 5px;
    --box-shadow: 2px 2px 10px;
    --color-accent: #118bee15;
    --color-bg: #fff;
    --color-bg-secondary: #e9e9e9;
    --color-link: #118bee;
    --color-secondary: #920de9;
    --color-secondary-accent: #920de90b;
    --color-shadow: #f4f4f4;
    --color-table: #118bee;
    --color-text: #000;
    --color-text-secondary: #999;
    --font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Oxygen-Sans, Ubuntu, Cantarell, "Helvetica Neue", sans-serif;
    --hover-brightness: 1.2;
    --justify-important: center;
    --justify-normal: left;
    --line-height: 1.5;
    --width-card: 285px;
    --width-card-medium: 460px;
    --width-card-wide: 800px;
    --width-content: 1080px;
}
  


<!-- MVP.css quickstart template: https://github.com/andybrewer/mvp/ -->

<!DOCTYPE html>
<html lang="en">

<head>
    <link rel="icon" href="https://via.placeholder.com/70x70">
    <link rel="stylesheet" href="./mvp.css">

    <meta charset="utf-8">
    <meta name="description" content="My description">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">

    <title>My title</title>
</head>

<body>
    <header>
        <nav>
            <a href="/"><img alt="Logo" src="https://via.placeholder.com/200x70?text=Logo" height="70"></a>
            <ul>
                <li>Menu Item 1</li>
                <li><a href="#section-1">Menu Item 2</a></li>
                <li><a href="#">Dropdown Menu Item</a>
                    <ul>
                        <li><a href="#">Sublink with a long name</a></li>
                        <li><a href="#">Short sublink</a></li>
                    </ul>
                </li>
            </ul>
        </nav>
        <h1>Page Heading with <i>Italics</i> and <u>Underline</u></h1>
        <p>Page Subheading with <mark>highlighting</mark></p>
        <br>
        <p><a href="#"><i>Italic Link Button</i></a><a href="#"><b>Bold Link Button &rarr;</b></a></p>
    </header>
    <main>
        <hr>
        <section id="section-1">
            <header>
                <h2>Section Heading</h2>
                <p>Section Subheading</p>
            </header>
            <aside>
                <h3>Card heading</h3>
                <p>Card content*</p>
                <p><small>*with small content</small></p>
            </aside>
            <aside>
                <h3>Card heading</h3>
                <p>Card content <sup>with notification</sup></p>
            </aside>
            <aside>
                <h3>Card heading</h3>
                <p>Card content</p>
            </aside>
        </section>
        <hr>
        <section>
            <blockquote>
                "Quote"
                <footer><i>- Attribution</i></footer>
            </blockquote>
        </section>
        <hr>
        <section>
            <table>
                <thead>
                    <tr>
                        <th></th>
                        <th>Col A</th>
                        <th>Col B</th>
                        <th>Col C</th>
                    </tr>
                </thead>
                <tr>
                    <td>Row 1</td>
                    <td>Cell A1</td>
                    <td>Cell B1</td>
                    <td>Cell C1</td>
                </tr>
                <tr>
                    <td>Row 2</td>
                    <td>Cell A2</td>
                    <td>Cell B2</td>
                    <td>Cell C2</td>
                </tr>
            </table>
        </section>
        <hr>
        <article>
            <h2>Left-aligned header</h2>
            <p>Left-aligned paragraph</p>
            <aside>
                <p>Article callout</p>
            </aside>
            <ul>
                <li>List item 1</li>
                <li>List item 2</li>
            </ul>
            <figure>
                <img alt="Stock photo" src="https://via.placeholder.com/1080x500?text=Amazing+stock+photo">
                <figcaption><i>Image caption</i></figcaption>
            </figure>
        </article>
        <hr>
        <div>
            <details>
                <summary>Expandable title</summary>
                <p>Revealed content</p>
            </details>
            <details>
                <summary>Another expandable title</summary>
                <p>More revealed content</p>
            </details>
            <br>
            <p>Inline <code>code</code> snippets</p>
            <pre>
                <code>
// preformatted code block
                </code>
            </pre>
        </div>
        <hr>
        <section>
            <form>
                <header>
                    <h2>Form title</h2>
                </header>
                <label for="input1">Input label:</label>
                <input type="text" id="input1" name="input1" size="20" placeholder="Input1">
                <label for="select1">Select label:</label>
                <select id="select1">
                    <option value="option1">option1</option>
                    <option value="option2">option2</option>
                </select>
                <label for="textarea1">Textarea label:</label>
                <textarea cols="40" rows="5" id="textarea1"></textarea>
                <button type="submit">Submit</button>
            </form>
        </section>
    </main>
    <footer>
        <hr>
        <p>
            <small>Contact info</small>
        </p>
    </footer>
</body>

</html>
