// A stand-in for the TMDB API during end-to-end tests, so they need no TMDB account and give the same
// answers every run. It knows two films and answers every list and search with them.
import { createServer } from "node:http";

const films = [
  { id: 900001, title: "The Test Pattern", overview: "A film that only exists for tests.", release_date: "2020-05-01", runtime: 101 },
  { id: 900002, title: "Second Reel", overview: "Another test film.", release_date: "2021-09-17", runtime: 94 },
];

const summary = (f) => ({ id: f.id, title: f.title, overview: f.overview, release_date: f.release_date, poster_path: null, backdrop_path: null, vote_average: 7.2, genre_ids: [] });
const list = (results) => ({ page: 1, results, total_pages: 1, total_results: results.length });

function answer(path) {
  const movie = path.match(/^\/3\/movie\/(\d+)(\/[a-z]+)?$/);
  if (movie) {
    const film = films.find((f) => f.id === Number(movie[1]));
    if (!film) return [404, { status_message: "Not found" }];
    switch (movie[2]) {
      case undefined:
        return [200, { ...summary(film), runtime: film.runtime, genres: [], vote_count: 10, original_title: film.title, original_language: "en", tagline: null, imdb_id: null }];
      case "/credits":
        return [200, { id: film.id, cast: [], crew: [] }];
      case "/videos":
        return [200, { id: film.id, results: [] }];
      case "/images":
        return [200, { id: film.id, backdrops: [], posters: [], logos: [] }];
      default:
        return [200, list(films.filter((f) => f.id !== film.id).map(summary))];
    }
  }

  if (/^\/3\/(trending\/movie\/week|movie\/popular|search\/movie|discover\/movie)$/.test(path)) return [200, list(films.map(summary))];
  if (/^\/3\/search\/(person|company)$/.test(path)) return [200, list([])];
  if (path === "/3/configuration") return [200, { images: {} }];
  return [404, { status_message: "Not found" }];
}

createServer((request, response) => {
  const [status, body] = answer(new URL(request.url, "http://fake").pathname);
  response.writeHead(status, { "Content-Type": "application/json" });
  response.end(JSON.stringify(body));
}).listen(8080, () => console.log("Fake TMDB listening on 8080"));
