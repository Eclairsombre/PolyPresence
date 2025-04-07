using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using backend.Data;
using backend.Models;

namespace backend.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudents()
        {
            return await _context.Students.ToListAsync();
        }
        [HttpGet("{studentNumber}")]
        public async Task<ActionResult<Student>> GetStudentByStudentNumber(string studentNumber)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);

            if (student == null)
            {
                return NotFound();
            }

            return student;
        }

        [HttpPost]
        public async Task<ActionResult<Student>> PostStudent(Student student)
        {
            var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.StudentNumber == student.StudentNumber);
            if (existingStudent != null)
            {
                return Conflict(new { message = "A student with this student number already exists." });
            }

            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetStudentByStudentNumber), new { studentNumber = student.StudentNumber }, student);
        }

        [HttpPut("{studentNumber}")]
        public async Task<IActionResult> PutStudent(string studentNumber, Student student)
        {
            if (studentNumber != student.StudentNumber)
            {
                return BadRequest();
            }

            var existingStudent = await _context.Students.FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);
            if (existingStudent == null)
            {
                return NotFound();
            }

            _context.Entry(existingStudent).CurrentValues.SetValues(student);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!StudentExistsByStudentNumber(studentNumber))
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

        private bool StudentExistsByStudentNumber(string studentNumber)
        {
            return _context.Students.Any(e => e.StudentNumber == studentNumber);
        }
        [HttpDelete("{studentNumber}")]
        public async Task<IActionResult> DeleteStudentByStudentNumber(string studentNumber)
        {
            var student = await _context.Students.FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);
            if (student == null)
            {
                return NotFound();
            }

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpGet("search/{studentNumber}")]
        public async Task<IActionResult> SearchByStudentNumber(string studentNumber)
        {
            var exists = await _context.Students.AnyAsync(s => s.StudentNumber == studentNumber);
            return Ok(new { exists });
        }
        private bool StudentExists(int id)
        {
            return _context.Students.Any(e => e.Id == id);
        }

        [HttpGet("fetch/{year}")]
        public async Task<ActionResult<IEnumerable<Student>>> GetStudentsByYear(string year)
        {
            var students = await _context.Students.Where(s => s.Year == year).ToListAsync();
            if (students == null || students.Count == 0)
            {
                return NotFound(new { message = "No students found for the specified year." });
            }
            return students;
        }
    }
}