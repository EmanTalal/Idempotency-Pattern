using Microsoft.AspNetCore.Mvc;

namespace IdempotencyProject.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class InvoiceContoller : ControllerBase
    {
        private static readonly List<Invoice> _invoice = [];

        [HttpPost]
        [Idempotent]
        public IActionResult CreateInvoice([FromBody] Invoice invoice)
        {
            Thread.Sleep(2000); //SimlateProccing Time
            invoice.Id = Guid.NewGuid();
            _invoice.Add(invoice);
            return CreatedAtAction(nameof(GetInvoice),new {id=invoice.Id }, invoice);
        }
        [HttpGet("id")]
        public  IActionResult GetInvoice(Guid id)
        {
            var invoice = _invoice.FirstOrDefault(x => x.Id == id);
            if (invoice == null)
            {
                return NotFound();
            }
            return Ok(invoice);
        }

    }

    public class Invoice
    {
        public Guid Id { get; set; }
        public string CustomerName { get; set; }
        public decimal Amount { get; set; }
    }
}
