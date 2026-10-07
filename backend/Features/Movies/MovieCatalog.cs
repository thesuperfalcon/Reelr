using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.Movies
{
    // The local copy of TMDB films. A film is stored the first time someone logs, rates, reviews or lists it.
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

            movie = new Movie
            {
                TmdbId = tmdbId,
                Title = tmdbMovie.Title ?? string.Empty,
                Description = tmdbMovie.Overview,
                ReleaseDate = DateOnly.TryParse(tmdbMovie.ReleaseDate, out var releaseDate) ? releaseDate : null,
                Runtime = tmdbMovie.Runtime,
                PosterUrl = tmdbMovie.PosterPath,
                BackdropUrl = tmdbMovie.BackdropPath
            };

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
    }
}
