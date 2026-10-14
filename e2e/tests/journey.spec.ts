import { expect, test } from "@playwright/test";
import { logIn, password, signUp, uniqueName } from "./accounts";

// One member's whole life on Reelr: sign up, log a film with a review, follow someone, delete the account.
// The films come from the fake TMDB in e2e/fake-tmdb.
test("sign up, log a film with a review, follow someone and delete the account", async ({ page, browser }) => {
  // Someone to follow, signed up in a separate browser session.
  const friend = uniqueName("friend");
  const friendContext = await browser.newContext();
  await signUp(await friendContext.newPage(), friend);
  await friendContext.close();

  const member = uniqueName("member");
  await signUp(page, member);
  await expect(page.getByRole("link", { name: `Profile of ${member}` })).toBeVisible();

  // Log a film with a rating and a review.
  await page.goto("/movie/900001");
  await expect(page.getByRole("heading", { level: 1, name: "The Test Pattern" })).toBeVisible();
  await page.getByRole("button", { name: /^Rate The Test Pattern/ }).click();
  const dialog = page.getByRole("dialog");
  const stars = dialog.getByRole("slider");
  await stars.focus();
  await page.keyboard.press("End");
  await page.keyboard.press("ArrowLeft");
  await expect(stars).toHaveAttribute("aria-valuetext", "4.5 out of 5 stars");
  const review = `Sharp and strange, in a good way. (${member})`;
  await dialog.getByLabel("Review (optional)").fill(review);
  await dialog.getByRole("button", { name: "Save" }).click();
  await expect(dialog).toBeHidden();

  await expect(page.getByText(review)).toBeVisible();
  await expect(page.getByRole("button", { name: /Your rating: 4.5 out of 5 stars/ })).toBeVisible();

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
