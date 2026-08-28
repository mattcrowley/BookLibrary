using Asp.Versioning;
using BookLibrary.Api.Data;
using BookLibrary.Api.Models;
using BookLibrary.Infrastructure.Services.External;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BookLibrary.Api.Controllers.V2
{
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")] // removes number from any controller name
    public class BookRetrievalController : ControllerBase
    {
        private readonly AppDbContext _context;
        private OpenLibraryService _openLibraryService;

        public BookRetrievalController(
            AppDbContext context,
            OpenLibraryService openLibraryService)
        {
            _context = context;
            _openLibraryService = openLibraryService;
        }

        // TODO: This is an interesting way to not specify route of each method
        //[HttpGet("by-field")]
        //public IActionResult GetCustomerProfileByField()
        //how to calculate it: [Controller Base Route] + / + [Attribute Path]The Resulting Endpoint: GET /api/v1/customer-profiles/by-field

        // GET: api/Controller/works-filtered
        [HttpGet("works-filtered")]
        [MapToApiVersion("2.0")]
        //[Route("get-works")]
        public async Task<ActionResult<IEnumerable<Works>>> GetWorks()
        {
            var books = await _context.Works.AsNoTracking()
                .Select(c => new Works
                {
                    Id = c.Id,
                    Title = c.Title,
                    Subtitle = c.Subtitle,
                    RawJson = c.RawJson,
                    LastModified = c.LastModified,
                    OLKey = c.OLKey
                }
            )
            .OrderBy(c => c.Id)
            .Take(100).ToListAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };

            // Now parse our json for more fields
            //foreach (var book in books)
            //{
            //    if (string.IsNullOrWhiteSpace(book.RawJson))
            //        continue;
            //    var parsedJson = JsonSerializer.Deserialize<WorksJsonParsedInfo>(book.RawJson, options);

            //    book.WorksJsonInfo = parsedJson;
            //}
            return books;
        }

        [HttpGet]
        [Route("get-works-by-search-direct-api")]
        public async Task<ActionResult<string>> GetWorksFromOpenLibraryApi([FromQuery] string searchTerm)
        {
            var result = "";

            result = await _openLibraryService.SearchWorks(searchTerm);

            return result;
        }

        [HttpGet]
        [Route("get-works-with-description")]
        public async Task<ActionResult<IEnumerable<WorkSummaryDTO>>> GetWorksByDescription()
        {
            // Where has to be done first since our data does not have the raw json parsed
            var books = await _context.Works
            .Where(c => c.RawJson.Description.Value != null && c.RawJson.Description.Value != "")
            .Select(c => new WorkSummaryDTO
            (
                c.Id,
                c.Title,
                c.Subtitle,
                c.RawJson.Description.Value,
                c.LastModified,
                c.OLKey
            ))
            .Take(10)
            .ToListAsync();

            return books;
        }

        // GET: api/Book/5
        [HttpGet("{id}")]
        public async Task<ActionResult<Book>> GetBook(int id)
        {
            var book = await _context.Books.FindAsync(id);

            if (book == null)
            {
                return NotFound();
            }

            return book;
        }

        // PUT: api/Book/5
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPut("{id}")]
        public async Task<IActionResult> PutBook(int? id, Book book)
        {
            if (id != book.Id)
            {
                return BadRequest();
            }

            _context.Entry(book).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        // POST: api/Book
        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Book>> PostBook(Book book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBook", new { id = book.Id }, book);
        }

        // DELETE: api/Book/5
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBook(int? id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound();
            }

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool BookExists(int? id)
        {
            return _context.Books.Any(e => e.Id == id);
        }
    }
}