using Asp.Versioning;
using BookLibrary.Api.Constants;
using BookLibrary.Api.Data;
using BookLibrary.Api.Models;
using BookLibrary.Infrastructure.Services.External;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace BookLibrary.Api.Controllers.V2
{
    /// <summary>
    /// Uses Entity Framework to query our DB for any data
    /// </summary>
    [ApiController]
    [ApiVersion("2.0")]
    [Route("api/v{version:apiVersion}/[controller]")] // note to remember: versioning removes number from any controller name if number is at the end ex BookRetrievalV2X -> BookRetrievalVX
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

        [HttpGet("works-filtered")]
        [MapToApiVersion("2.0")]
        //[Authorize(AuthenticationSchemes = AppConstants.ApiKeySchemeName)] // API Key only
        //[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)] // JWT
        [Authorize(Policy = AppConstants.AuthenticationDualAuthPolicy)] // Custom scheme supporting both
        public async Task<ActionResult<IEnumerable<Works>>> GetWorks()
        {
            var books = await _context.Works
                .AsNoTracking()
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

            // TODO: I am planning on these being new fields, not storing raw json anymore, since it has lots of data
            // and querying it requires manual parsing, or more complex logic to parse it via EF
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
            // The Where statement has to be done first since our data does not have the raw json parsed
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

        [HttpGet("{id}")]
        public async Task<ActionResult<Works>> GetBook(int id)
        {
            var book = await _context.Works.FindAsync(id);

            if (book == null)
            {
                return NotFound();
            }

            return book;
        }

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

        // To protect from overposting attacks, see https://go.microsoft.com/fwlink/?linkid=2123754
        [HttpPost]
        public async Task<ActionResult<Book>> PostBook(Book book)
        {
            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            return CreatedAtAction("GetBook", new { id = book.Id }, book);
        }

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