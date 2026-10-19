import { afterAll, describe, expect, it, vi } from "vitest";

// A viewer in Stockholm, where 23:30 UTC on 18 October is already 01:30 on the 19th.
// The time zone is set before lib/dates.ts is loaded, since its formatters read it when they are made.
vi.stubEnv("TZ", "Europe/Stockholm");
const dates = await import("./dates");

const nearMidnight = "2026-10-18T23:30:00Z";

describe("dates", () => {
  afterAll(() => {
    vi.unstubAllEnvs();
  });

  it("shows moments in the viewer's time zone", () => {
    expect(dates.formatDate(nearMidnight)).toBe("Oct 19, 2026");
    expect(dates.formatDateLong(nearMidnight)).toBe("October 19, 2026");
    expect(dates.formatDateTime(nearMidnight)).toMatch(/^October 19, 2026 at 1:30\sAM$/);
  });

  it("keeps calendar days on the UTC day, wherever the viewer is", () => {
    expect(dates.formatDay(nearMidnight)).toBe("Oct 18, 2026");
    expect(dates.formatDayLong(nearMidnight)).toBe("October 18, 2026");
    expect(dates.formatDayFull(nearMidnight)).toBe("Sunday, October 18, 2026");
    expect(dates.formatDayOfMonth(nearMidnight)).toBe("18");
    expect(dates.formatWeekday(nearMidnight)).toBe("Sunday");
    expect(dates.formatWeekdayShort(nearMidnight)).toBe("Sun");
    expect(dates.formatMonth("2026-10-31T23:30:00Z")).toBe("October 2026");
    expect(dates.formatMonthShort("2026-10-31T23:30:00Z")).toBe("Oct");
  });

  it("accepts the times the API and lib/stats.ts use", () => {
    const day = Date.parse("2026-10-18T00:00:00Z");

    expect(dates.formatDayLong(day)).toBe("October 18, 2026");
    expect(dates.formatDayLong(new Date(day))).toBe("October 18, 2026");
  });
});
