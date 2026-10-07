using backend.Data;
using backend.Features.Auth;
using backend.Features.Movies;
using backend.Features.MovieLists.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Features.MovieLists
{
    [ApiController]
    [Route("api/lists")]
    public class MovieListController : ControllerBase
    {
        private const int TopMoviesCount = 3;

        private readonly ReelrContext _context;
        private readonly MovieCatalog _movieCatalog;

        public MovieListController(ReelrContext context, MovieCatalog movieCatalog)
        {
            _context = context;
            _movieCatalog = movieCatalog;
        }

        [Authorize]
        [HttpGet]
        [EndpointSummary("Get the current user's movie lists")]
        public async Task<ActionResult<List<MovieListSummaryDto>>> GetLists()
        {
            var userId = User.GetUserId();

            var lists = await ToSummaries(_context.MovieLists.Where(l => l.UserId == userId))
                .ToListAsync();

            return Ok(lists);
        }

        [HttpGet("/api/users/{userId:int}/lists")]
        [EndpointSummary("Get a user's movie lists. Private lists are included only for their owner")]
        public async Task<ActionResult<List<MovieListSummaryDto>>> GetUserLists(int userId)
        {
            if (!await _context.Users.AnyAsync(u => u.Id == userId))
            {
                return NotFound();
            }

            var own = User.FindUserId() == userId;

            var lists = await ToSummaries(_context.MovieLists.Where(l => l.UserId == userId && (own || l.IsPublic)))
                .ToListAsync();

            return Ok(lists);
        }

        [Authorize]
        [HttpGet("containing/{tmdbId:int}")]
        [EndpointSummary("Get the ids of the current user's lists that contain a movie")]
        public async Task<ActionResult<List<int>>> GetListsContaining(int tmdbId)
        {
            var userId = User.GetUserId();

            var ids = await _context.MovieListItems
                .Where(i => i.MovieList.UserId == userId && i.Movie.TmdbId == tmdbId)
                .Select(i => i.MovieListId)
                .ToListAsync();

            return Ok(ids);
        }

        [HttpGet("{id:int}")]
        [EndpointSummary("Get a movie list")]
        public async Task<ActionResult<MovieListDto>> GetList(int id)
        {
            var list = await _context.MovieLists
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (list == null)
            {
                return NotFound();
            }

            if (!list.IsPublic && !IsOwner(list))
            {
                return NotFound();
            }

            var topMovies = await _context.MovieListItems
                .Where(i => i.MovieListId == id)
                .OrderByDescending(i => i.AddedAt)
                .Take(TopMoviesCount)
                .Select(i => new MovieListItemDto
                {
                    TmdbId = i.Movie.TmdbId,
                    Title = i.Movie.Title,
                    PosterUrl = i.Movie.PosterUrl,
                    AddedAt = i.AddedAt
                })
                .ToListAsync();

            var movieCount = await _context.MovieListItems.CountAsync(i => i.MovieListId == id);

            return Ok(ToDto(list, movieCount, topMovies));
        }

        [HttpGet("{id:int}/movies")]
        [EndpointSummary("Get the movies in a list")]
        public async Task<ActionResult<List<MovieListItemDto>>> GetListMovies(int id)
        {
            var list = await _context.MovieLists.FirstOrDefaultAsync(l => l.Id == id);

            if (list == null)
            {
                return NotFound();
            }

            if (!list.IsPublic && !IsOwner(list))
            {
                return NotFound();
            }

            var movies = await _context.MovieListItems
                .Where(i => i.MovieListId == id)
                .OrderByDescending(i => i.AddedAt)
                .Select(i => new MovieListItemDto
                {
                    TmdbId = i.Movie.TmdbId,
                    Title = i.Movie.Title,
                    PosterUrl = i.Movie.PosterUrl,
                    AddedAt = i.AddedAt
                })
                .ToListAsync();

            return Ok(movies);
        }

        [Authorize]
        [HttpPost]
        [EndpointSummary("Create a movie list")]
        public async Task<ActionResult<MovieListDto>> CreateList(CreateMovieListDto dto)
        {
            var userId = User.GetUserId();

            var list = new MovieList
            {
                UserId = userId,
                Name = dto.Name,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                CreatedAt = DateTime.UtcNow
            };

            _context.MovieLists.Add(list);
            await _context.SaveChangesAsync();

            await _context.Entry(list).Reference(l => l.User).LoadAsync();

            return CreatedAtAction(nameof(GetList), new { id = list.Id }, ToDto(list, 0, []));
        }

        [Authorize]
        [HttpPut("{id:int}")]
        [EndpointSummary("Update a movie list")]
        public async Task<ActionResult<MovieListDto>> UpdateList(int id, UpdateMovieListDto dto)
        {
            var userId = User.GetUserId();

            var list = await _context.MovieLists
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

            if (list == null)
            {
                return NotFound();
            }

            if (dto.Name != null)
            {
                list.Name = dto.Name;
            }

            if (dto.Description != null)
            {
                list.Description = dto.Description;
            }

            if (dto.IsPublic.HasValue)
            {
                list.IsPublic = dto.IsPublic.Value;
            }

            list.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var topMovies = await _context.MovieListItems
                .Where(i => i.MovieListId == id)
                .OrderByDescending(i => i.AddedAt)
                .Take(TopMoviesCount)
                .Select(i => new MovieListItemDto
                {
                    TmdbId = i.Movie.TmdbId,
                    Title = i.Movie.Title,
                    PosterUrl = i.Movie.PosterUrl,
                    AddedAt = i.AddedAt
                })
                .ToListAsync();

            var movieCount = await _context.MovieListItems.CountAsync(i => i.MovieListId == id);

            return Ok(ToDto(list, movieCount, topMovies));
        }

        [Authorize]
        [HttpDelete("{id:int}")]
        [EndpointSummary("Delete a movie list")]
        public async Task<IActionResult> DeleteList(int id)
        {
            var userId = User.GetUserId();

            var list = await _context.MovieLists
                .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

            if (list == null)
            {
                return NotFound();
            }

            _context.MovieLists.Remove(list);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpPost("{id:int}/movies")]
        [EndpointSummary("Add a movie to a list")]
        public async Task<IActionResult> AddMovie(int id, AddMovieToListDto dto)
        {
            var userId = User.GetUserId();

            var list = await _context.MovieLists
                .FirstOrDefaultAsync(l => l.Id == id && l.UserId == userId);

            if (list == null)
            {
                return NotFound();
            }

            var movie = await _movieCatalog.GetOrCreateAsync(dto.TmdbId);

            if (movie == null)
            {
                return NotFound();
            }

            var alreadyInList = await _context.MovieListItems
                .AnyAsync(i => i.MovieListId == id && i.MovieId == movie.Id);

            if (alreadyInList)
            {
                return Conflict("Movie is already in the list.");
            }

            _context.MovieListItems.Add(new MovieListItem
            {
                MovieListId = id,
                MovieId = movie.Id,
                AddedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id:int}/movies/{tmdbId:int}")]
        [EndpointSummary("Remove a movie from a list")]
        public async Task<IActionResult> RemoveMovie(int id, int tmdbId)
        {
            var userId = User.GetUserId();

            var listExists = await _context.MovieLists.AnyAsync(l => l.Id == id && l.UserId == userId);

            if (!listExists)
            {
                return NotFound();
            }

            var item = await _context.MovieListItems
                .FirstOrDefaultAsync(i => i.MovieListId == id && i.Movie.TmdbId == tmdbId);

            if (item == null)
            {
                return NotFound();
            }

            _context.MovieListItems.Remove(item);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // Newest list first, each with its newest movies as a preview.
        private static IQueryable<MovieListSummaryDto> ToSummaries(IQueryable<MovieList> lists) =>
            lists
                .OrderByDescending(l => l.CreatedAt)
                .Select(l => new MovieListSummaryDto
                {
                    Id = l.Id,
                    Name = l.Name,
                    IsPublic = l.IsPublic,
                    MovieCount = l.Items.Count,
                    TopMovies = l.Items
                        .OrderByDescending(i => i.AddedAt)
                        .Take(TopMoviesCount)
                        .Select(i => new MovieListItemDto
                        {
                            TmdbId = i.Movie.TmdbId,
                            Title = i.Movie.Title,
                            PosterUrl = i.Movie.PosterUrl,
                            AddedAt = i.AddedAt
                        })
                        .ToList(),
                    CreatedAt = l.CreatedAt
                });

        private bool IsOwner(MovieList list)
        {
            return User.FindUserId() == list.UserId;
        }

        private static MovieListDto ToDto(MovieList list, int movieCount, List<MovieListItemDto> topMovies)
        {
            return new MovieListDto
            {
                Id = list.Id,
                UserId = list.UserId,
                Username = list.User.UserName ?? string.Empty,
                Name = list.Name,
                Description = list.Description,
                IsPublic = list.IsPublic,
                CreatedAt = list.CreatedAt,
                UpdatedAt = list.UpdatedAt,
                MovieCount = movieCount,
                TopMovies = topMovies
            };
        }
    }
}
