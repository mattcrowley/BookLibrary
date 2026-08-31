using Asp.Versioning;
using BookLibrary.Api.Models;
using BookLibrary.Infrastructure.DatabaseAccess;
using Microsoft.AspNetCore.Mvc;

namespace BookLibrary.Api.Controllers.V1
{
    /// <summary>
    /// This uses ADO.NET queries for retrieving results. 
    /// See V2 controller that uses entity framework instead.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class BookRetrievalController : ControllerBase
    {
        private Database _databaseAccess;
        public BookRetrievalController(Database db, DatabaseOptions options)
        {
            _databaseAccess = new Database(options, log: msg => Console.WriteLine(msg));
        }

        // GET: api/<BookRetrievalController>
        [HttpGet]
        public IEnumerable<string> Get()
        {
            return new string[] { "value1", "value2" };
        }

        // GET api/<BookRetrievalController>/5
        [HttpGet("{id}")]
        public async Task<List<WorkSummaryDTO>> GetAsync(int id)
        {
            var count = await _databaseAccess.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM Works where Id = ${id};");

            if (count > 0)
            {
                var listOfWorks = await _databaseAccess.ExecuteReaderAsync(
                    $"SELECT TOP 1000 Id, Title, Subtitle FROM Works where Id = ${id};",
                    reader =>
                    {
                        // Since some of these fields (subtitle especially) can be null, we need to be able to handle that
                        // so we need to use getOrdinal and isDBNull methods
                        int ordinal = reader.GetOrdinal("Title");
                        string title = reader.IsDBNull(ordinal) ? "No title" : reader.GetString(ordinal);
                        ordinal = reader.GetOrdinal("Subtitle");
                        string subtitle = reader.IsDBNull(ordinal) ? "No subtitle" : reader.GetString(ordinal);
                        return new WorkSummaryDTO(
                            reader.GetInt32(0),
                            title,
                            subtitle, "", null, "");
                    });

                return listOfWorks;
            }
            else
            {
                return [];
            }
        }

        // POST api/<BookRetrievalController>
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        // PUT api/<BookRetrievalController>/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        // DELETE api/<BookRetrievalController>/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
