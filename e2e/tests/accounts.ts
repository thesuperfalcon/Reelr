import { expect, type APIRequestContext, type Page } from "@playwright/test";

const mailpit = process.env.E2E_MAILPIT_URL ?? "http://localhost:8025";

export const password = "E2e-Passw0rd!";

export function uniqueName(prefix: string): string {
  return `${prefix}_${Date.now().toString(36)}${Math.floor(Math.random() * 1000)}`;
}

// Waits for the confirmation email in Mailpit and returns the link in it.
export async function confirmationLink(request: APIRequestContext, email: string): Promise<string> {
  for (let attempt = 0; attempt < 40; attempt++) {
    const search = await request.get(`${mailpit}/api/v1/search`, { params: { query: `to:"${email}"` } });
    const { messages } = (await search.json()) as { messages: { ID: string }[] };

    if (messages.length > 0) {
      const message = (await (await request.get(`${mailpit}/api/v1/message/${messages[0].ID}`)).json()) as { Text: string };
      const link = message.Text.match(/https?:\/\/\S+\/confirm-email#\S+/)?.[0];
      if (link) {
        return link;
      }
    }

    await new Promise((resolve) => setTimeout(resolve, 250));
  }

  throw new Error(`No confirmation email reached ${email}.`);
}

// Registers through the form, opens the link from the email, and logs in.
export async function signUp(page: Page, username: string): Promise<void> {
  const email = `${username}@e2e.test`;

  await page.goto("/register");
  await page.getByLabel("Username").fill(username);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password", { exact: true }).fill(password);
  await page.getByLabel("Password again", { exact: true }).fill(password);
  await page.getByRole("checkbox").check();
  await page.getByRole("button", { name: "Create account" }).click();
  await expect(page.getByRole("heading", { name: "Check your inbox" })).toBeVisible();

  await page.goto(await confirmationLink(page.request, email));
  await expect(page.getByRole("heading", { name: "Email confirmed" })).toBeVisible();

  await logIn(page, username);
}

export async function logIn(page: Page, username: string, secret = password): Promise<void> {
  await page.goto("/login");
  await page.getByLabel("Username or email").fill(username);
  await page.getByLabel("Password", { exact: true }).fill(secret);
  await page.getByRole("button", { name: "Log in" }).click();
}
