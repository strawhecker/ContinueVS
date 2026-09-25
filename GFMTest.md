# GFM Test Battery

A comprehensive set of GitHub Flavored Markdown tests. No LaTeX math.

---

## 1. Headings

# H1
## H2
### H3
#### H4
##### H5
###### H6

Also a fake setext heading style:

Setext H1
=========

Setext H2
---------

---

## 2. Emphasis (bold / italic / strikethrough)

*italic*
_italic too_
**bold**
__bold too__
***bold italic***
~~strikethrough~~
_You can **nest** them_
Unemphasized asterisks: a*b*c, and 2^10 for clarity.

**Note:** underscores *inside words* like snake_case should **not** be emphasized.

---

## 3. Links

Inline: [GitHub](https://github.com)
With title: [Example](https://example.com "Example site")

Reference style:

[GFM spec][gfm]
[gfm]: https://github.github.com/gfm/

Autolink: <https://github.com> and <mailto:test@example.com>

Bare www autolink: www.google.com and https://example.org (should link automatically in GFM).

---

## 4. Images

Inline: ![Alt text](https://github.githubassets.com/images/modules/logos_page/GitHub-Mark.png)

With title: ![Octocat](https://github.com/octocat.png "The Octocat")

---

## 5. Lists

Unordered:

- Apples
- Oranges
- Pears
  - nested item
  - another nested
    - deeper still
- Back to top level

Ordered:

1. First
2. Second
3. Third
   1. nested one
   2. nested two
4. Fourth

Task list (GFM):

- [x] Done item
- [ ] Undone item
- [ ] Another undone
  - [ ] Nested task

Mixed lazy list where numbers don't matter:

3) three
1) one
8) eight

---

## 6. Fenced code blocks

```python
def hello(name):
    """Greet someone."""
    print(f"Hello, {name}!")

hello("world")
```

```json
{
  "name": "GFM Test",
  "enabled": true,
  "numbers": [1, 2, 3],
  "nested": {"key": "value"}
}
```

```csharp
using System;

public class Greeter
{
    public static string Greet(string name) =>
        $"Hi, {name}!";
}
```

No language specified:

```
This is a plain fenced block with no language.
```

Tilde fences also work:

~~~text
Tilde-style fenced code block.
~~~

---

## 7. Tables

Simple table:

| Name | Role | Level |
|------|------|-------|
| Alice | Admin | 10 |
| Bob | User | 3 |
| Charlie | Guest | 1 |

Alignment (GFM):

| Left | Center | Right |
|:-----|:------:|------:|
| a | b | c |
| 1 | 22 | 333 |
| longer | text | here |

Table with code and emphasis:

| Feature | Notes |
|---------|-------|
| `inline code` | **bold** cell |
| links | [Example](https://example.com) |

---

## 8. Blockquotes

> This is a blockquote.
> It can span multiple lines.
>
> > And contain *nested* blockquotes.
>
> - with lists
> - inside too

---

## 9. Inline code

Use `printf` to print. The backtick `` ` `` is a literal. Code with spaces `` ` a b ` `` works in GFM.

---

## 10. Horizontal rules

---

***

___

---

## 11. Line breaks & hard breaks

This line has two trailing spaces at the end→  
so this is a hard break (new line, not new paragraph).

This is a backslash break at end→\
also a hard break.

A normal line return
without trailing spaces just continues (soft wrap) in the same paragraph.

---

## 12. Escaping special characters

\*not italic\*  \_not emphasized\_  \# not a heading  \`not code\`  \~not strikethrough\~  \[not a link\](x)  \<not a tag\>

---

## 13. Mentions, refs, emoji (GitHub)

- Mention: @strawhecker
- Issue/PR ref: #42
- Commit SHA: abc1234
- Emoji: :rocket: :tada: :smile: :+1: :shipit:
- Unicode emoji: 🚀 🎉 😄 👍

---

## 14. HTML blocks (GFM raw HTML)

<details>
  <summary>Click to expand</summary>
  This is an HTML `<details>` block.
  Text here is **raw HTML**, not markdown.
</details>

<p>This is a raw <em>HTML</em> paragraph. <span style="color:red">Red span</span>.</p>

---

## 15. Footnotes (GFM)

Here's a sentence with a footnote.[^1]

[^1]: This is the footnote definition.

---

## 16. Alerts (GitHub callouts)

> [!NOTE]
> Useful information that users should know, even when skimming.

> [!TIP]
> Helpful advice for doing things better or more easily.

> [!IMPORTANT]
> Key information users need to know.

> [!WARNING]
> Urgent info that needs immediate user attention.

> [!CAUTION]
> Advises about risks or negative outcomes.

---

## 17. Strikethrough + task combos

- [x] ~~completed and strikethrough~~
- [ ] a `bold` **task** with *formatting*

---

## 18. Odd / edge cases

Nested emphasis: ***this is bold-italic***

```

This entire line is code because the fence directly follows.

```

Escaped fence \`\`\` not code.

Two spaces before newline is a hard break  
after this.

Text with `multiple` **bold** *styles* ~~mixed~~ [links](https://github.com) all on one line.

---

*Battery complete.* 🎉
