import { useSearchParams } from "react-router";
import { Poster } from "../components/Poster";
import { ErrorMessage, Loading } from "../components/Status";
import { useSearchAll } from "../lib/queries";
import { tmdbImage } from "../lib/tmdb";
import type { Person } from "../lib/types";

function PeopleList({ title, people }: { title: string; people: Person[] }) {
  if (people.length === 0) {
    return null;
  }

  return (
    <section aria-label={title} className="mt-12">
      <h2 className="marquee text-3xl">{title}</h2>
      <ul className="mt-5 grid grid-cols-2 gap-x-4 gap-y-5 sm:grid-cols-3 lg:grid-cols-4">
        {people.slice(0, 12).map((person) => {
          const photo = tmdbImage(person.profile_path, "w185");
          return (
            <li key={person.id} className="flex items-center gap-3">
              {photo ? (
                <img src={photo} alt="" loading="lazy" className="size-12 shrink-0 rounded-full object-cover" />
              ) : (
                <div className="size-12 shrink-0 rounded-full bg-row" aria-hidden="true" />
              )}
              <div className="min-w-0 text-sm">
                <p className="truncate font-medium">{person.name}</p>
                {person.known_for_department && <p className="truncate text-haze">{person.known_for_department}</p>}
              </div>
            </li>
          );
        })}
      </ul>
    </section>
  );
}

export function SearchPage() {
  const [params] = useSearchParams();
  const query = params.get("q")?.trim() ?? "";
  const search = useSearchAll(query);

  return (
    <div className="mx-auto max-w-6xl px-4 py-10 sm:px-6">
      {query ? (
        <h1 className="marquee text-5xl sm:text-6xl">Results for “{query}”</h1>
      ) : (
        <h1 className="marquee text-5xl sm:text-6xl">Search</h1>
      )}

      {!query && <p className="mt-4 text-haze">Type a film title, a name or a studio in the search field above.</p>}

      {query && search.isPending && <Loading label="Searching" />}
      {search.isError && <ErrorMessage error={search.error} retry={() => search.refetch()} />}

      {search.data && (
        <>
          {search.data.movies.length === 0 &&
            search.data.cast.length === 0 &&
            search.data.crew.length === 0 &&
            search.data.studios.length === 0 && (
              <p className="mt-6 text-haze">Nothing matches “{query}”. Check the spelling or try a shorter search.</p>
            )}

          {search.data.movies.length > 0 && (
            <section aria-label="Films" className="mt-10">
              <h2 className="marquee text-3xl">Films</h2>
              <ul className="mt-5 grid grid-cols-3 gap-3 sm:grid-cols-4 md:grid-cols-6 lg:gap-4">
                {search.data.movies.map((movie) => (
                  <li key={movie.id}>
                    <Poster movie={movie} size="w342" />
                  </li>
                ))}
              </ul>
            </section>
          )}

          <PeopleList title="Cast" people={search.data.cast} />
          <PeopleList title="Crew" people={search.data.crew} />

          {search.data.studios.length > 0 && (
            <section aria-label="Studios" className="mt-12">
              <h2 className="marquee text-3xl">Studios</h2>
              <ul className="mt-5 flex flex-wrap gap-x-6 gap-y-2 text-sm">
                {search.data.studios.slice(0, 20).map((studio) => (
                  <li key={studio.id}>{studio.name}</li>
                ))}
              </ul>
            </section>
          )}
        </>
      )}
    </div>
  );
}
