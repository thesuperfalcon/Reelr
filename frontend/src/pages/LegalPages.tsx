import type { ReactNode } from "react";
import { Link } from "react-router";
import { usePageTitle } from "../lib/title";

// These texts describe what the code and the Azure setup (infra/) do as of this version. Keep them in step:
// a change to what is stored, where, or for how long belongs here too. They have not been reviewed by a lawyer,
// which is fine for the closed beta; have them reviewed before Reelr opens to the public.

const contact = "reelr.beta@outlook.com";

function LegalShell({ title, updated, children }: { title: string; updated: string; children: ReactNode }) {
  return (
    <article className="page py-10 *:max-w-3xl sm:py-14">
      <h1 className="heading-page">{title}</h1>
      <p className="mt-3 text-sm text-haze">Last updated {updated}</p>
      <p role="note" className="mt-6 rounded-sm border-l-2 border-projector bg-row p-4 text-sm">
        Reelr is in a closed beta for invited members.
      </p>
      <div className="legal-text mt-8 space-y-4 text-sm leading-relaxed [&_h2]:marquee [&_h2]:mt-10 [&_h2]:text-3xl [&_li]:ml-5 [&_li]:list-disc">
        {children}
      </div>
    </article>
  );
}

export function PrivacyPage() {
  usePageTitle("Privacy", "What Reelr stores about you, why, who else sees it, and how to see, take with you or delete it.");
  return (
    <LegalShell title="Privacy" updated="14 October 2026">
      <p>
        This page explains what Reelr stores about you, why, who else sees it, and how you can see, take with you or
        delete it. Reelr is run by Tintin Falk, a private person in Sweden, who is responsible for your personal data.
        Contact: {contact}.
      </p>

      <h2>What we store</h2>
      <ul>
        <li>Your account: username, email address, a hash of your password (never the password itself), and when you accepted these terms.</li>
        <li>Your profile picture, if you upload one. It is redrawn on our server, so location data from your camera is not kept.</li>
        <li>What you do on Reelr: diary entries, ratings, watched films, watchlist, reviews, comments, likes, lists, who you follow, who you block, and reports you send.</li>
        <li>Your settings, such as who can see your diary and watchlist.</li>
        <li>The invite you signed up with, and the note the admin wrote on it, usually your name.</li>
        <li>Your logins: one record per signed-in browser, so a login can be ended, for at most 30 days.</li>
      </ul>
      <p>
        Your IP address is kept in memory for a short time to limit how often requests can be sent, which protects
        accounts against password guessing. Our servers also write it to their logs, together with the time and the
        page asked for, to find faults and attacks. The logs are kept for 30 days.
      </p>

      <h2>Why</h2>
      <ul>
        <li>To provide the service you signed up for: your diary, reviews, lists and the social features (the agreement with you).</li>
        <li>To keep Reelr safe: limiting login attempts, revoking logins, and handling reports (our legitimate interest in a safe service).</li>
        <li>To send emails about your account: confirming your address and resetting your password. We send no newsletters or marketing.</li>
      </ul>

      <h2>Who else sees it</h2>
      <ul>
        <li>
          Other people see what you publish, as your settings allow: reviews, comments, public lists and your profile are
          visible to everyone; your diary and watchlist to everyone, your followers or only you, as you choose.
        </li>
        <li>
          Film posters and photos come straight from TMDB (The Movie Database), so your browser contacts image.tmdb.org
          and TMDB sees your IP address. Search terms and film lookups are sent to TMDB by our server, without anything
          that identifies you.
        </li>
        <li>Account emails are sent through Brevo, an email service in the EU, which therefore handles your email address.</li>
        <li>Reelr runs on Microsoft Azure in Sweden. Your data, logs and backups stay there.</li>
        <li>Admins see reports and the reported content. The author of reported content is not told who reported it.</li>
      </ul>
      <p>
        We count how often each kind of page is opened per day, such as "a film page" or "the search page", to see
        what is used. The count is kept on our own server and holds nothing else: no address with an id in it, no
        account, no IP address and no cookie, so it cannot be tied to you. If your browser sends Do Not Track or
        Global Privacy Control, your visits are not counted at all.
      </p>
      <p>We do not sell your data and we use no advertising services or outside analytics or tracking services.</p>

      <h2>Cookies and storage in your browser</h2>
      <p>
        Reelr sets one cookie, only when you log in: it keeps you logged in for up to 30 days, is sent only to Reelr's
        login service, and cannot be read by scripts on the page. Local storage in your browser remembers that you are
        logged in and the theme you chose; session storage keeps unsaved review text until you close the tab. All of
        this is needed for the site to work, so we do not ask for consent. There are no tracking cookies.
      </p>

      <h2>How long</h2>
      <p>
        We keep your data as long as you have an account. When you delete your account, your profile, diary, ratings,
        reviews, comments, likes, lists, follows, blocks and reports are deleted at once. Backups are kept for 7 days
        before they are deleted too.
      </p>

      <h2>Your rights</h2>
      <ul>
        <li>See and take your data with you: Settings, then "Download my data".</li>
        <li>Correct it: change your username, picture and settings in Settings, or edit your diary, reviews and lists.</li>
        <li>Delete it: Settings, then "Delete account".</li>
        <li>Object or ask questions: write to {contact}.</li>
        <li>
          Complain to the Swedish Authority for Privacy Protection (IMY), imy.se, if you think we handle your data
          wrongly.
        </li>
      </ul>

      <h2>Age</h2>
      <p>You must be at least 13 to create an account.</p>
    </LegalShell>
  );
}

// How to reach the person who runs Reelr. Keep the ways to see, download and delete data in step with
// the privacy page.
export function ContactPage() {
  usePageTitle("Contact", "How to reach the person who runs Reelr, and what to write about.");
  return (
    <LegalShell title="Contact" updated="14 October 2026">
      <p>
        Reelr is run by Tintin Falk, a private person in Sweden. Write to{" "}
        <a href={`mailto:${contact}`} className="link">
          {contact}
        </a>{" "}
        about anything to do with Reelr, in English or Swedish.
      </p>

      <h2>What to write about</h2>
      <ul>
        <li>Something that does not work, or is hard to use with your keyboard, screen reader or magnifier. Say which page and what you tried.</li>
        <li>Your personal data: questions, objections, or help with seeing, correcting or deleting it.</li>
        <li>Invites, ideas and anything else.</li>
      </ul>

      <h2>Faster ways for some things</h2>
      <ul>
        <li>Download your data or delete your account: Settings, then "Download my data" or "Delete account".</li>
        <li>A review, comment or list that breaks the rules: use its "Report" link, so admins see exactly what you mean.</li>
      </ul>
    </LegalShell>
  );
}

// Describes what the frontend does for accessibility. Keep it in step with the code: a feature listed here
// that is removed, or a new one, belongs here too.
export function AccessibilityPage() {
  usePageTitle(
    "Accessibility",
    "How Reelr works with a keyboard, a screen reader or a magnifier, what does not work yet, and how to report problems.",
  );
  return (
    <LegalShell title="Accessibility" updated="13 October 2026">
      <p>
        Reelr should work for everyone, whether you use a mouse, a keyboard, a screen reader or a magnifier. We aim for
        the WCAG 2.2 guidelines at level AA. This page says what we have done, what we know does not work yet, and how
        to tell us about problems.
      </p>

      <h2>Keyboard</h2>
      <ul>
        <li>Everything can be done with the keyboard alone, and the control in focus always has a visible outline.</li>
        <li>The first Tab on every page shows a "Skip to content" link that jumps past the menu.</li>
        <li>When you go to another page, focus moves to the new page's content, and the browser tab names the page.</li>
        <li>Dialogs keep focus inside them while open, close with Escape, and give focus back to the button that opened them.</li>
        <li>When an inline step closes, such as a delete confirmation or an edit form, focus goes back to the button you started from.</li>
        <li>Star ratings move in half stars with the arrow keys; Home gives half a star and End gives five.</li>
        <li>Tabs, such as on profiles, move with the arrow keys, Home and End.</li>
        <li>The review editor has shortcuts for bold (Ctrl+B), italic (Ctrl+I) and links (Ctrl+K), or ⌘ on a Mac.</li>
      </ul>

      <h2>Screen readers</h2>
      <ul>
        <li>Pages use headings, landmarks and lists, so you can jump between sections.</li>
        <li>Every form field has a label, help text is read with its field, and errors are read out as soon as they appear.</li>
        <li>Messages such as "Saved" or a failed request are announced without moving focus.</li>
        <li>Ratings are read as text, for example "4.5 out of 5 stars".</li>
        <li>Reviews with spoilers stay hidden until you choose to show them; then focus moves to the review text.</li>
      </ul>

      <h2>Seeing and motion</h2>
      <ul>
        <li>
          Five looks to choose from in Settings, two of them light (Broadsheet and Video Store). Your choice is kept in
          your browser and works before you log in.
        </li>
        <li>Text can be enlarged with the browser's zoom without content being cut off.</li>
        <li>If your system asks for less motion, Reelr turns off fades, slides and loading animations.</li>
      </ul>

      <h2>How we check</h2>
      <p>
        Every change is checked automatically: the axe accessibility rules run on our forms and on the main pages in a
        real browser, including colour contrast, and tests confirm that keyboard focus behaves as described above.
      </p>

      <h2>Known problems</h2>
      <ul>
        <li>Posters and photos from TMDB have no text description; the film or person's name is always shown next to them.</li>
        <li>Film information comes from TMDB and is only in English.</li>
        <li>Reelr has not been reviewed by an outside accessibility expert or tested with every screen reader.</li>
      </ul>

      <h2>Tell us</h2>
      <p>
        If something is hard or impossible to use, write to {contact} and say which page and what you tried. We will
        answer and fix what we can.
      </p>
    </LegalShell>
  );
}

export function TermsPage() {
  usePageTitle("Terms", "The terms for using Reelr: your account, what you post, moderation, film data and the service.");
  return (
    <LegalShell title="Terms" updated="12 October 2026">
      <p>
        These terms apply when you use Reelr. By creating an account you accept them and the{" "}
        <Link to="/privacy" className="link">
          privacy policy
        </Link>
        . Reelr is run by Tintin Falk, a private person in Sweden, {contact}.
      </p>

      <h2>Your account</h2>
      <ul>
        <li>You must be at least 13 years old.</li>
        <li>Keep your password to yourself. You are responsible for what happens with your account.</li>
        <li>One person per account. Usernames that pretend to be someone else may be changed or removed.</li>
      </ul>

      <h2>What you post</h2>
      <ul>
        <li>You keep the rights to your reviews, comments and lists. You let Reelr show them on the site as your settings allow.</li>
        <li>Mark reviews that give away the plot as containing spoilers.</li>
        <li>
          Do not post spam, harassment, hate, threats, other people's personal data, or anything illegal. Do not post
          text you do not have the right to share.
        </li>
      </ul>

      <h2>Moderation</h2>
      <p>
        Anyone can report a review, comment or list. Admins may remove content that breaks these terms, and may close
        accounts that break them repeatedly or seriously. You can block members you do not want to hear from.
      </p>

      <h2>Film data</h2>
      <p>
        Film information and images come from TMDB. Reelr uses the TMDB API but is not endorsed or certified by TMDB.
      </p>

      <h2>The service</h2>
      <p>
        Reelr is provided as it is, without guarantees that it is always available or free of errors. You can stop using
        it and delete your account at any time. We may change these terms; if a change matters, we will say so on the
        site before it applies.
      </p>
    </LegalShell>
  );
}
