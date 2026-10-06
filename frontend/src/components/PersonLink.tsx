import { Link } from "react-router";
import { tmdbImage } from "../lib/tmdb";

interface PersonLinkProps {
  id: number;
  name: string | null;
  profilePath: string | null;
  /** Second line, such as the character played or the person's department. */
  detail?: string | null;
}

// Photo, name and an optional detail line as one link to the person's page.
export function PersonLink({ id, name, profilePath, detail }: PersonLinkProps) {
  const photo = tmdbImage(profilePath, "w185");

  return (
    <Link to={`/person/${id}`} className="group flex items-center gap-3 rounded-sm">
      {photo ? (
        <img src={photo} alt="" loading="lazy" className="size-12 shrink-0 rounded-full object-cover" />
      ) : (
        <div className="size-12 shrink-0 rounded-full bg-row" aria-hidden="true" />
      )}
      <div className="min-w-0 text-sm">
        <p className="truncate font-medium group-hover:text-projector">{name}</p>
        {detail && <p className="truncate text-haze">{detail}</p>}
      </div>
    </Link>
  );
}
