﻿using CaridentMedix.Server.Models;

namespace CaridentMedix.Server.Controllers.Admin;

public class AdminUserEditRequest
{
    public bool? IsClinicAdmin { get; set; }

    public bool? IsDeleted { get; set; }

    public Dentist? Dentist { get; set; }

    public string? Email { get; set; }

    public string? Name { get; set; }
}