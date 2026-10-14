import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { useAuth } from "../auth/auth-context";
import { ReportButton } from "../components/ReportButton";
import { LockIcon } from "../components/ListCard";
import { ListFormDialog } from "../components/ListFormDialog";
import { LoadMore } from "../components/LoadMore";
import { Poster } from "../components/Poster";
import { EmptyState, ErrorMessage, Loading, PosterGridSkeleton } from "../components/Status";
import { UserLink } from "../components/UserAvatar";
import { ApiError } from "../lib/api";
import { pageItems, useDeleteMovieList, useMovieList, useMovieListMovies, useToggleListMovie, useWatchedFilms } from "../lib/queries";
import type { MovieList, MovieListItem, SearchMovie } from "../lib/types";
import { NotFoundPage } from "./NotFoundPage";
import { usePageTitle } from "../lib/title";
import { useReturnFocus } from "../lib/focus";

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
      <EmptyState
        action={
          owner && (
            <Link to="/" className="btn btn-secondary">
              Find films
            </Link>
          )
        }
      >
        <p>{owner ? "This list is empty. Add films with the list button on a film's page." : "This list is empty."}</p>
      </EmptyState>
    );
  }

  return (
    <>
      {remove.isError && (
        <p role="alert" className="mt-6 rounded-sm border-l-2 border-alarm bg-row px-3 py-2 text-sm">
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
                    className="absolute top-1 right-1 flex size-9 items-center justify-center rounded-full bg-salon/85 text-screen ring-1 ring-rule/20 backdrop-blur-sm transition hover:bg-alarm hover:text-salon"
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

// Only mounted for logged-in visitors, since the watched films need a token.
function WatchedMovieGrid(props: { list: MovieList; movies: MovieListItem[]; owner: boolean }) {
  const watchedFilms = useWatchedFilms();
  const watched = watchedFilms.data ?? new Set<number>();
  const { list } = props;

  return (
    <>
      {list.watchedCount !== null && list.movieCount > 0 && (
        <p className="mt-6 text-sm text-haze">
          You have watched <span className="font-semibold text-screen">{list.watchedCount}</span> of {list.movieCount}.
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
  const deleteRef = useReturnFocus(confirmDelete);

  return (
    <div className="mt-6">
      <div className="flex flex-wrap items-center gap-3 text-sm">
        <button
          type="button"
          onClick={() => setEditing(true)}
          aria-haspopup="dialog"
          className="btn btn-sm btn-secondary"
        >
          Edit list
        </button>
        {confirmDelete ? (
          <span className="flex flex-wrap items-center gap-x-3 gap-y-1">
            <span className="text-haze">Delete “{list.name}” for good?</span>
            <button
              type="button"
              onClick={() =>
                remove.mutate(list.id, { onSuccess: () => navigate("/profile?tab=lists", { replace: true }) })
              }
              disabled={remove.isPending}
              className="link-danger"
            >
              {remove.isPending ? "Deleting…" : "Delete"}
            </button>
            <button
              type="button"
              autoFocus
              onClick={() => setConfirmDelete(false)}
              disabled={remove.isPending}
              className="link-quiet"
            >
              Keep
            </button>
          </span>
        ) : (
          <button
            ref={deleteRef}
            type="button"
            onClick={() => setConfirmDelete(true)}
            className="link-quiet hover:text-alarm"
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
  usePageTitle(
    details.data?.name,
    details.data && (details.data.description || `A film list by ${details.data.username} on Reelr.`),
  );
  const movies = useMovieListMovies(listId);

  if (!Number.isInteger(listId) || listId <= 0 || (details.error instanceof ApiError && details.error.status === 404)) {
    return <NotFoundPage />;
  }

  if (details.isPending) {
    return (
      <div className="page">
        <Loading label="Loading list" />
      </div>
    );
  }

  if (details.isError) {
    return (
      <div className="page">
        <ErrorMessage error={details.error} retry={() => details.refetch()} />
      </div>
    );
  }

  const list = details.data;
  const owner = user?.id === list.userId;

  return (
    <article className="page py-10 sm:py-14">
      <header>
        <div className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm text-haze">
          <span className="eyebrow">List</span>
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
        <h1 className="heading-display mt-4">{list.name}</h1>
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
        {user && !owner && (
          <p className="mt-4 text-sm">
            <ReportButton targetType="List" targetId={list.id} what="this list" />
          </p>
        )}
      </header>

      {movies.isPending ? (
        <PosterGridSkeleton count={6} label="Loading films" className="mt-8" />
      ) : movies.isError ? (
        <ErrorMessage error={movies.error} retry={() => movies.refetch()} />
      ) : (
        <>
          {user ? (
            <WatchedMovieGrid list={list} movies={pageItems(movies.data)} owner={owner} />
          ) : (
            <MovieGrid list={list} movies={pageItems(movies.data)} owner={false} watched={new Set()} />
          )}
          <LoadMore query={movies} what="films" />
        </>
      )}
    </article>
  );
}
