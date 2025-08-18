namespace AcademicResourceApp.DTOs
{
    public class BorrowRequestDto
    {
        public int Id { get; set; }
        public int ResourceId { get; set; }
        public string ResourceTitle { get; set; }
        public Guid BorrowerId { get; set; }
        public string BorrowerName { get; set; }
        public string BorrowerEmail { get; set; }
        public string Status { get; set; }
        public DateTime RequestDate { get; set; }
    }
}
