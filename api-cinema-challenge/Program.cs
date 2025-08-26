using api_cinema_challenge.Data;
using api_cinema_challenge.DTOs.CustomerDTOs;
using api_cinema_challenge.DTOs.MovieDTOs;
using api_cinema_challenge.DTOs.ScreeningDTOs;
using api_cinema_challenge.DTOs.TicketDTOs;
using api_cinema_challenge.Endpoints;
using api_cinema_challenge.Models;
using api_cinema_challenge.Repository;
using api_cinema_challenge.Services;
using api_cinema_challenge.Validators.CustomerValidators;
using api_cinema_challenge.Validators.MovieValidators;
using api_cinema_challenge.Validators.ScreeningValidators;
using api_cinema_challenge.Validators.TicketValidators;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using System.Diagnostics;
using System.Text;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "Test API", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter a valid token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] { }
        }
    });
}); 

builder.Services.AddProblemDetails();

builder.Services.AddRouting(options => options.LowercaseUrls = true);
builder.Services.AddDbContext<CinemaContext>(options => {
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnectionString"));
    options.LogTo(message => Debug.WriteLine(message));
});

builder.Services.AddScoped<IRepository<Customer>, Repository<Customer>>();
builder.Services.AddScoped<IRepository<Movie>, Repository<Movie>>();
builder.Services.AddScoped<IRepository<Screening>, Repository<Screening>>();
builder.Services.AddScoped<IRepository<Ticket>, Repository<Ticket>>();
// Register validators as services
builder.Services.AddScoped<IValidator<CustomerPostDto>, CustomerPostValidator>();
builder.Services.AddScoped<IValidator<CustomerPutDto>, CustomerPutValidator>();
builder.Services.AddScoped<IValidator<MoviePostDto>, MoviePostValidator>();
builder.Services.AddScoped<IValidator<MoviePutDto>, MoviePutValidator>();
builder.Services.AddScoped<IValidator<ScreeningPostDto>, ScreeningPostValidator>();
builder.Services.AddScoped<IValidator<TicketPostDto>, TicketPostValidator>();
// Register Token Service
builder.Services.AddScoped<TokenService, TokenService>();


// Support string to enum conversions
builder.Services.AddControllers().AddJsonOptions(opt =>
{
    opt.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});


// Specify identity requirements
// Must be added before .AddAuthentication otherwise a 404 is thrown on authorized endpoints
builder.Services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = false;
        options.User.RequireUniqueEmail = true;
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<CinemaContext>();

// These will eventually be moved to a secrets file, but for alpha development appsettings is fine
var validIssuer = builder.Configuration.GetValue<string>("JwtTokenSettings:ValidIssuer");
var validAudience = builder.Configuration.GetValue<string>("JwtTokenSettings:ValidAudience");
var symmetricSecurityKey = builder.Configuration.GetValue<string>("JwtTokenSettings:SymmetricSecurityKey");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
    .AddJwtBearer(options =>
    {
        options.IncludeErrorDetails = true;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ClockSkew = TimeSpan.Zero,
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = validIssuer,
            ValidAudience = validAudience,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(symmetricSecurityKey)
            ),
        };
    });

// policy-based authorization for Admin and User roles
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
        policy.RequireRole("Admin"));
    options.AddPolicy("User", policy =>
        policy.RequireRole("User"));
});

// build the app
var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStatusCodePages();

app.UseAuthentication();
app.UseAuthorization();

app.ConfigureCustomerEndpoint();
app.ConfigureMovieEndpoint();

app.MapControllers();
app.Run();
