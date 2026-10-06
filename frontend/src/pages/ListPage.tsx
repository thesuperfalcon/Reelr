import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { LockIcon } from "../components/ListCard";
import { ListFormDialog } from "../components/ListFormDialog";
import { Poster } from "../components/Poster";
import { ErrorMessage, Loading } from "../components/Status";
import { UserLink } from "../components/UserAvatar";
import { ApiError } from "../lib/api";
import { useDeleteMovieList, useDiary, useMovieList, useMovieListMovies, useToggleListMovie } from "../lib/queries";
import type { MovieList, MovieListItem, SearchMovie } from "../lib/types";
import { NotFoundPage } from "./NotFoundPage";

const dateFormat = new Intl.DateTimeFormat("en", { day: "numeric", month: "long", year: "numeric" });

function asSearchMovie(item: MovieListItem): SearchMovie {
  return {
    id: item.tmdbId,
    title: item.title,
    poster_path: item.posterUrl,
    overview: null,
    release_date: null,
    vote_average: 0,
  };
}

function MovieGrid({
  list,
  movies,
  owner,
  watched,
}: {
  list: MovieList;
  movies: MovieListItem[];
  owner: boolean;
  watched: Set<number>;
}) {
  const remove = useToggleListMovie();

  if (movies.length === 0) {
    return (
      <p className="py-10 text-haze">
        {owner ? "This list is empty. Add films with the list button on a film's page." : "This list is empty."}
      </p>
    );
  }

  return (
    <>
      {remove.isError && (
        <p role="alert" className="mt-6 text-sm text-alarm">
          {remove.error.message}
        </p>
      )}
      <ul className="mt-8 grid grid-cols-3 gap-x-3 gap-y-6 sm:grid-cols-4 md:grid-cols-6 lg:gap-x-4">
        {movies.map((movie) => {
          const removing = remove.isPending && remove.variables?.tmdbId === movie.tmdbId;
          return (
            <li key={movie.tmdbId} className={removing ? "opacity-50" : ""}>
              <div className="relative">
                <Poster movie={asSearchMovie(movie)} size="w342" />
                {watched.has(movie.tmdbId) && (
                  <span
                    className="pointer-events-none absolute left-1.5 top-1.5 flex size-6 items-center justify-center rounded-full bg-projector text-salon shadow"
                    title="You have watched this"
                  >
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="3" strokeLinecap="round" strokeLinejoin="round" className="size-3.5" aria-hidden="true">
                      <path d="M5 12l5 5 9-10" />
                    </svg>
                    <span className="sr-only">Watched</span>
                  </span>
                )}
                {owner && (
                  <button
                    type="button"
                    onClick={() => remove.mutate({ listId: list.id, tmdbId: movie.tmdbId, inList: true })}
                    disabled={removing}
                    aria-label={`Remove ${movie.title} from ${list.name}`}
                    title="Remove from list"
                    className="absolute right-1.5 top-1.5 flex size-7 items-center justify-center rounded-full bg-salon/80 text-screen ring-1 ring-white/20 transition hover:bg-alarm hover:text-salon"
                  >
                    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" className="size-3.5" aria-hidden="true">
                      <path d="M6 6l12 12M18 6L6 18" />
                    </svg>
                  </button>
                )}
              </div>
              <p className="mt-2 truncate text-sm" title={movie.title}>
                {movie.title}
              </p>
            </li>
          );
        })}
      </ul>
    </>
  );
}

// Only mounted for logged-in visitors, since the diary needs a token.
function WatchedMovieGrid(props: { list: MovieList; movies: MovieListItem[]; owner: boolean }) {
  const diary = useDiary();
  const watched = new Set((diary.data ?? []).map((entry) => entry.tmdbId));
  const seen = props.movies.filter((movie) => watched.has(movie.tmdbId)).length;

  return (
    <>
      {diary.isSuccess && props.movies.length > 0 && (
        <p className="mt-6 text-sm text-haze">
          You have watched <span className="font-semibold text-screen">{seen}</span> of {props.movies.length}.
        </p>
      )}
      <MovieGrid {...props} watched={watched} />
    </>
  );
}

function OwnerActions({ list }: { list: MovieList }) {
  const navigate = useNavigate();
  const remove = useDeleteMovieList();
  const [editing, setEditing] = useState(false);
  const [confirmDelete, setConfirmDelete] = useState(false);

  return (
    <div className="mt-6">
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <button
          type="button"
          onClick={() => setEditing(true)}
          aria-haspopup="dialog"
          className="inline-flex h-9 items-center rounded-sm px-3 font-medium text-screen ring-1 ring-white/15 transition hover:bg-row"
        >
          Edit list
        </button>
        {confirmDelete ? (
          <span className="flex items-center gap-3">
            <span className="text-haze">Delete “{list.name}” for good?</span>
            <button
              type="button"
              onClick={() =>
                remove.mutate(list.id, { onSuccess: () => navigate("/profile?tab=lists", { replace: true }) })
              }
              disabled={remove.isPending}
              className="font-medium text-alarm underline underline-offset-4 disabled:opacity-50"
            >
              {remove.isPending ? "Deleting…" : "Delete"}
            </button>
            <button
              type="button"
              onClick={() => setConfirmDelete(false)}
              disabled={remove.isPending}
              className="text-haze underline underline-offset-4 hover:text-screen"
            >
              Keep
            </button>
          </span>
        ) : (
          <button
            type="button"
            onClick={() => setConfirmDelete(true)}
            className="text-haze underline underline-offset-4 transition hover:text-alarm"
          >
            Delete list
          </button>
        )}
      </div>
      {remove.isError && (
        <p role="alert" className="mt-2 text-sm text-alarm">
          {remove.error.message}
        </p>
      )}
      {editing && <ListFormDialog current={list} onClose={() => setEditing(false)} />}
    </div>
  );
}

export function ListPage() {
  const listId = Number(useParams().listId);
  const { user } = useAuth();
  const details = useMovieList(listId);
  const movies = useMovieListMovies(listId);

  if (!Number.isInteger(listId) || listId <= 0 || (details.error instanceof ApiError && details.error.status === 404)) {
    return <NotFoundPage />;
  }

  if (details.isPending) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <Loading label="Loading list" />
      </div>
    );
  }

  if (details.isError) {
    return (
      <div className="mx-auto max-w-6xl px-4 sm:px-6">
        <ErrorMessage error={details.error} retry={() => details.refetch()} />
      </div>
    );
  }

  const list = details.data;
  const owner = user?.id === list.userId;

  return (
    <article className="mx-auto max-w-6xl px-4 py-10 sm:px-6 sm:py-14">
      <header>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-haze">
          <span className="text-projector">List</span>
          <UserLink
            user={{ id: list.userId, userName: list.username, profileImageUrl: null }}
            avatarClassName="size-7 text-base"
          />
          {!list.isPublic && (
            <span className="inline-flex items-center gap-1.5">
              <LockIcon />
              Private
            </span>
          )}
        </div>
        <h1 className="marquee mt-4 text-6xl wrap-break-word sm:text-8xl">{list.name}</h1>
        {list.description && <p className="mt-6 max-w-prose whitespace-pre-line text-screen/90">{list.description}</p>}
        <p className="mt-4 text-sm text-haze">
          {list.movieCount} {list.movieCount === 1 ? "film" : "films"} · Created{" "}
          <time dateTime={list.createdAt}>{dateFormat.format(new Date(list.createdAt))}</time>
          {list.updatedAt && (
            <>
              {" "}
              · Updated <time dateTime={list.updatedAt}>{dateFormat.format(new Date(list.updatedAt))}</time>
            </>
          )}
        </p>
        {owner && <OwnerActions list={list} />}
      </header>

      {movies.isPending ? (
        <Loading label="Loading films" />
      ) : movies.isError ? (
        <ErrorMessage error={movies.error} retry={() => movies.refetch()} />
      ) : user ? (
        <WatchedMovieGrid list={list} movies={movies.data} owner={owner} />
      ) : (
        <MovieGrid list={list} movies={movies.data} owner={false} watched={new Set()} />
      )}
    </article>
  );
}
