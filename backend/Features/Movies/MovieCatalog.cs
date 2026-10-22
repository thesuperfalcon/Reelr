using backend.Data;
using backend.Features.Movies.DTOs;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Movies
{
    // The local copy of TMDB films. A film is stored the first time someone logs, rates, reviews or lists it,
    // and MaintenanceLoop copies it again now and then, since titles, posters and runtimes change on TMDB.
    public class MovieCatalog
    {
        private readonly ReelrContext _context;
        private readonly TmdbService _tmdbService;

        public MovieCatalog(ReelrContext context, TmdbService tmdbService)
        {
            _context = context;
            _tmdbService = tmdbService;
        }

        // Returns the stored film, fetching and saving it from TMDB first when needed.
        // Null when TMDB does not know the id. Saves straight away, so call it before making other changes.
        public async Task<Movie?> GetOrCreateAsync(int tmdbId)
        {
            var movie = await _context.Movies.FirstOrDefaultAsync(m => m.TmdbId == tmdbId);

            if (movie != null)
            {
                return movie;
            }

            var tmdbMovie = await _tmdbService.GetMovie(tmdbId);

            if (tmdbMovie == null)
            {
                return null;
            }

            movie = new Movie { TmdbId = tmdbId };
            CopyFrom(tmdbMovie, movie);

            _context.Movies.Add(movie);

            try
            {
                await _context.SaveChangesAsync();
                return movie;
            }
            catch (DbUpdateException)
            {
                // Another request stored the same film first and the unique TmdbId index refused this one.
                _context.Entry(movie).State = EntityState.Detached;

                return await _context.Movies.FirstOrDefaultAsync(m => m.TmdbId == tmdbId)
                    ?? throw new InvalidOperationException($"Could not store movie {tmdbId}.");
            }
        }

        /// <summary>
        /// Copies the stalest films from TMDB again, oldest copy first, and returns how many were looked at.
        /// A film TMDB no longer knows keeps its data. A film TMDB fails on stays due for the next run; a few
        /// failures in a row mean TMDB itself is down, and the run stops there.
        /// </summary>
        public async Task<int> RefreshStaleAsync(TimeSpan maxAge, int batchSize, CancellationToken cancellationToken)
        {
            var cutoff = DateTime.UtcNow - maxAge;
            var movies = await _context.Movies
                .Where(m => m.RefreshedAt < cutoff)
                .OrderBy(m => m.RefreshedAt)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            const int MaxFailuresInARow = 3;
            var done = 0;
            var failuresInARow = 0;

            foreach (var movie in movies)
            {
                TmdbMovieDto? tmdbMovie;

                try
                {
                    tmdbMovie = await _tmdbService.GetMovie(movie.TmdbId);
                    failuresInARow = 0;
                }
                catch (TmdbUnavailableException)
                {
                    if (++failuresInARow == MaxFailuresInARow)
                    {
                        break;
                    }

                    continue;
                }

                if (tmdbMovie != null)
                {
                    CopyFrom(tmdbMovie, movie);
                }

                movie.RefreshedAt = DateTime.UtcNow;
                done++;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return done;
        }

        private static void CopyFrom(TmdbMovieDto tmdbMovie, Movie movie)
        {
            movie.Title = Cut(tmdbMovie.Title ?? string.Empty, Movie.TitleMaxLength);
            movie.Description = tmdbMovie.Overview == null ? null : Cut(tmdbMovie.Overview, Movie.DescriptionMaxLength);
            movie.ReleaseDate = DateOnly.TryParse(tmdbMovie.ReleaseDate, out var releaseDate) ? releaseDate : null;
            movie.Runtime = tmdbMovie.Runtime;
            movie.PosterUrl = ImagePath(tmdbMovie.PosterPath);
            movie.BackdropUrl = ImagePath(tmdbMovie.BackdropPath);
            movie.RefreshedAt = DateTime.UtcNow;
        }

        private static string Cut(string text, int maxLength) =>
            text.Length <= maxLength ? text : text[..maxLength];

        // A shortened path would point at the wrong image, so one that does not fit is left out.
        private static string? ImagePath(string? path) =>
            path != null && path.Length <= Movie.ImagePathMaxLength ? path : null;
    }
}
