using DAL.DataModels;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DAL.ViewModels
{
    public class CreateRequestViewModel
    {
        [Required(ErrorMessage = "First name cannot be kept empty")]
        [RegularExpression("^[a-zA-Z]+$", ErrorMessage = "Please enter a valid first name.")]
        public string firstname {  get; set; }
        [RegularExpression("^[a-zA-Z]+$", ErrorMessage = "Please enter a valid last name.")]
        public string lastname { get; set; }
        [Required(ErrorMessage = "Phone number cannot be kept empty")]
        [RegularExpression(@"^\(?([0-9]{3})\)?[-. ]?([0-9]{3})[-. ]?([0-9]{4})$", ErrorMessage = "Entered phone format is not valid.")]
        public string phoneno { get; set; } = "";
        [Required(ErrorMessage = "Email cannot be kept empty")]
        [RegularExpression("^([\\w\\.\\-]+)@([\\w\\-]+)((\\.(\\w){2,3})+)$", ErrorMessage = "Enter Valid Email")]
        public string email {  get; set; }
        [Required(ErrorMessage = "Date of Birth cannot be empty")]
        public DateOnly birthDate { get; set; }
        public string street { get; set; }
        public string city { get; set; }
        [Required(ErrorMessage = "Kindly select a state")]
        public string state { get; set; }
        [StringLength(6)]
        public string zipcode { get; set; }
        public string room {  get; set; }

        public List<Region> regions { get; set; }
        public string adminNotes {  get; set; }
    }
}
