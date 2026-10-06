import Markdown from "react-markdown";
import remarkBreaks from "remark-breaks";

// Reviews are Markdown. Only inline formatting, quotes and lists render; headings and images
// fall back to their text. react-markdown never renders raw HTML and drops unsafe link URLs.
const allowedElements = ["p", "br", "strong", "em", "blockquote", "ul", "ol", "li", "a", "code", "hr"];

export function ReviewText({ text, className = "" }: { text: string; className?: string }) {
  return (
    <div className={`review-text ${className}`}>
      <Markdown
        remarkPlugins={[remarkBreaks]}
        allowedElements={allowedElements}
        unwrapDisallowed
        components={{
          a: ({ href, children }) => (
            <a href={href} target="_blank" rel="noopener noreferrer nofollow">
              {children}
            </a>
          ),
        }}
      >
        {text}
      </Markdown>
    </div>
  );
}
