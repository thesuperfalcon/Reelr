import { expect, test } from "@playwright/test";
import { logIn, password, signUp, uniqueName } from "./accounts";

// One member's whole life on Reelr: sign up, rate a film and review it, follow someone, delete the account.
// The films come from the fake TMDB in e2e/fake-tmdb.
test("sign up, rate and review a film, follow someone and delete the account", async ({ page, browser }) => {
  // Someone to follow, signed up in a separate browser session.
  const friend = uniqueName("friend");
  const friendContext = await browser.newContext();
  await signUp(await friendContext.newPage(), friend);
  await friendContext.close();

  const member = uniqueName("member");
  await signUp(page, member);
  await expect(page.getByRole("link", { name: `Profile of ${member}` })).toBeVisible();

  // Rate a film in one click on its page, then add a review in the log dialog.
  await page.goto("/movie/900001");
  await expect(page.getByRole("heading", { level: 1, name: "The Test Pattern" })).toBeVisible();
  const actions = page.getByRole("complementary", { name: "Your activity for this film" });
  const quickStars = actions.getByRole("slider", { name: "Your rating" });
  await quickStars.focus();
  await page.keyboard.press("End");
  await page.keyboard.press("ArrowLeft");
  await expect(quickStars).toHaveAttribute("aria-valuetext", "4.5 out of 5 stars");
  // A rated film counts as watched.
  await expect(actions.getByRole("button", { name: "Watched", pressed: true })).toBeVisible();

  await actions.getByRole("button", { name: "Review or log a date" }).click();
  const dialog = page.getByRole("dialog");
  await expect(dialog.getByRole("slider")).toHaveAttribute("aria-valuetext", "4.5 out of 5 stars");
  const review = `Sharp and strange, in a good way. (${member})`;
  await dialog.getByLabel("Review (optional)").fill(review);
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog).toBeHidden();

  await expect(page.getByText(review)).toBeVisible();
  // Still rated after a reload, so the server stored it.
  await page.reload();
  await expect(quickStars).toHaveAttribute("aria-valuetext", "4.5 out of 5 stars");

  // The log is in the diary.
  await page.goto("/profile");
  await expect(page.getByRole("link", { name: "The Test Pattern" }).first()).toBeVisible();

  // Find the friend and follow them.
  await page.getByRole("searchbox", { name: /Search/ }).fill(friend);
  await page.getByRole("searchbox", { name: /Search/ }).press("Enter");
  await page.getByRole("link", { name: friend }).first().click();
  await expect(page.getByRole("heading", { level: 1, name: friend })).toBeVisible();
  await page.getByRole("button", { name: "Follow", exact: true }).click();
  // The pressed button reads "Unfollow" while the pointer rests on it, "Following" otherwise.
  const following = page.getByRole("button", { name: /^(Following|Unfollow)$/, pressed: true });
  await expect(following).toBeVisible();
  // Still followed after a reload, so the server stored it.
  await page.reload();
  await expect(following).toBeVisible();

  // Delete the account.
  await page.goto("/settings");
  await page.getByRole("button", { name: "Delete account…" }).click();
  const deleteSection = page.getByRole("region", { name: "Delete account" });
  await deleteSection.getByRole("textbox", { name: "Password", exact: true }).fill(password);
  await deleteSection.getByRole("button", { name: "Delete my account" }).click();
  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("banner").getByRole("link", { name: "Log in" })).toBeVisible();

  // The account is gone, so its password no longer works.
  await logIn(page, member);
  await expect(page.getByRole("alert")).toContainText("Wrong username, email or password.");
});
