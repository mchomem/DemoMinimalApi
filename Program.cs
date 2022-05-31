using DemoMinimalApi.Data;
using DemoMinimalApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi.Models;
using MiniValidation;
using NetDevPack.Identity;
using NetDevPack.Identity.Jwt;
using NetDevPack.Identity.Model;

var builder = WebApplication.CreateBuilder(args);

#region Configure Services

builder.Services.AddDbContext<MinimalContextDb>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentityEntityFrameworkContextConfiguration(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
    b => b.MigrationsAssembly("DemoMinimalApi")));

builder.Services.AddIdentityConfiguration();
builder.Services.AddJwtConfiguration(builder.Configuration, "AppSettings");

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("DeleteProvider",
        policy => policy.RequireClaim("DeleteProvider"));
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Minimal API Sample",
        Description = "Developed based on the study project of Eduardo Pires - Owner @ desenvolvedor.io",
        Contact = new OpenApiContact { Name = "Misael C. Homem", Email = string.Empty },
        License = new OpenApiLicense { Name = "MIT", Url = new Uri("https://opensource.org/licenses/MIT") }
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "Enter the JWT token like this: Bearer {your token}",
        Name = "Authorization",
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            new string[] {}
        }
    });
});

var app = builder.Build();

#endregion

#region Configure Pipeline

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthConfiguration();
app.UseHttpsRedirection();

MapActions(app);

app.Run();

#endregion

#region Mapping end-points/actions

void MapActions(WebApplication app)
{
    #region User

    app.MapPost("/registry", [AllowAnonymous] async (
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings,
        RegisterUser registerUser
        ) =>
    {
        if (registerUser == null)
            return Results.BadRequest("Uninformed user");

        if (!MiniValidator.TryValidate(registerUser, out var erros))
            return Results.ValidationProblem(erros);

        var user = new IdentityUser
        {
            UserName = registerUser.Email,
            Email = registerUser.Email,
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(user, registerUser.Password);

        if (!result.Succeeded)
            return Results.BadRequest(result.Errors);

        var jwt = new JwtBuilder()
                        .WithUserManager(userManager)
                        .WithJwtSettings(appJwtSettings.Value)
                        .WithEmail(user.Email)
                        .WithJwtClaims()
                        .WithUserClaims()
                        .WithUserRoles()
                        .BuildUserResponse();

        return Results.Ok(jwt);
    })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PostRegistryUser")
        .WithTags("User");


    app.MapPost("/login", [AllowAnonymous] async (
        SignInManager<IdentityUser> signInManager,
        UserManager<IdentityUser> userManager,
        IOptions<AppJwtSettings> appJwtSettings,
        LoginUser loginUser) =>
    {
        if (loginUser == null)
            return Results.BadRequest("Uninformed user");

        if (!MiniValidator.TryValidate(loginUser, out var erros))
            return Results.ValidationProblem(erros);

        var result = await signInManager
                            .PasswordSignInAsync(loginUser.Email, loginUser.Password, false, true);

        if (result.IsLockedOut)
            return Results.BadRequest("Bloqued user");

        if (!result.Succeeded)
            return Results.BadRequest("Invalid user or password");

        var jwt = new JwtBuilder()
            .WithUserManager(userManager)
            .WithJwtSettings(appJwtSettings.Value)
            .WithEmail(loginUser.Email)
            .WithJwtClaims()
            .WithUserClaims()
            .WithUserRoles()
            .BuildUserResponse();

        return Results.Ok(jwt);
    })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PostLoginUser")
        .WithTags("User");

    #endregion

    #region Provider

    #region Get Methods

    app.MapGet("/provider", [AllowAnonymous] async (MinimalContextDb context) =>
        await context.Providers.ToListAsync())
        .WithName("GetAll")
        .WithTags("Provider"); // For swagger

    app.MapGet("/provider/{id}", [AllowAnonymous] async (Guid id, MinimalContextDb context) =>
        await context.Providers.FindAsync(id)
            is Provider provider
            ? Results.Ok(provider)
            : Results.NotFound())
        .Produces<Provider>(StatusCodes.Status200OK) // For swagger
        .Produces(StatusCodes.Status404NotFound)     // For swagger
        .WithName("GetProviderById")
        .WithTags("Provider");

    #endregion

    #region Post Methods

    app.MapPost("/provider", [Authorize] async (
        MinimalContextDb context,
        Provider provider) =>
    {
        if (!MiniValidator.TryValidate(provider, out var erros))
            return Results.ValidationProblem(erros);

        context.Providers.Add(provider);
        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.Created($"/provider/{provider.Id}", provider)
            : Results.BadRequest("There is a error on record save");
    })
        .ProducesValidationProblem() // Adicional documentation for swagger metadata
        .Produces<Provider>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PostProvider")
        .WithTags("Provider");

    #endregion

    #region Put Methods

    app.MapPut("/provider/{id}", [Authorize] async (
        Guid id,
        MinimalContextDb context,
        Provider provider) =>
    {
        var currentProvider = await context.Providers
            .AsNoTracking<Provider>()
            .FirstOrDefaultAsync(p => p.Id == id);

        if (currentProvider == null) return Results.NotFound();

        if (!MiniValidator.TryValidate(provider, out var erros))
            return Results.ValidationProblem(erros);

        context.Providers.Update(provider);
        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("There is a error on record save");
    })
        .ProducesValidationProblem()
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status400BadRequest)
        .WithName("PutProvider")
        .WithTags("Provider");

    #endregion

    #region Delete Methods

    app.MapDelete("/provider/{id}", [Authorize] async (
        Guid id,
        MinimalContextDb context) =>
    {
        var provider = await context.Providers.FindAsync(id);
        if (provider == null) return Results.NotFound();

        context.Providers.Remove(provider);
        var result = await context.SaveChangesAsync();

        return result > 0
            ? Results.NoContent()
            : Results.BadRequest("There is a error on record save");
    })
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status204NoContent)
        .Produces(StatusCodes.Status404NotFound)
        .RequireAuthorization("DeleteProvider")
        .WithName("DeleteProvider")
        .WithTags("Provider");

    #endregion

    #endregion
}

#endregion
