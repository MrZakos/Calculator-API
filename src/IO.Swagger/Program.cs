using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.OpenApi.Models;
using Microsoft.IdentityModel.Tokens;
using IO.Swagger.Filters;
using IO.Swagger.Middleware;
using IO.Swagger.Services;
using IO.Swagger.Services.Math;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Register Aspire ServiceDefaults & other services
builder.AddServiceDefaults();
builder.AddRedisClient(connectionName:"redis");
builder.Services.AddStackExchangeRedisOutputCache(options => {
	options.Configuration = builder.Configuration["Connection-Strings:redis"];
});
builder.Services.AddOutputCache();
builder.Services.AddSingleton<IMathService,MathService>();
builder.Services.AddSingleton<ICalculatorBusinessLogicService,CalculatorBusinessLogicService>();
builder.Services.AddSingleton<RedisService>();
builder.Services.AddScoped<IAuthenticationService,AuthenticationService>();
// Add Kafka services
builder.AddKafkaProducer<string,string>("kafka");
builder.Services.AddSingleton<IO.Swagger.Services.Kafka.IKafkaProducerService,IO.Swagger.Services.Kafka.KafkaProducerService>();
builder.Services.AddHostedService<IO.Swagger.Services.Kafka.KafkaConsumerService>();
// Add HttpContextAccessor for user context in services
builder.Services.AddHttpContextAccessor();
// Add framework services with System.Text.Json (replace Newtonsoft.Json)
builder.Services.AddControllers()
	   .AddJsonOptions(options => {
		   options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
		   options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
		   options.JsonSerializerOptions.WriteIndented = true;
	   })
	   .AddXmlSerializerFormatters();
// Configure JWT Authentication
var jwtSettings = builder.Configuration.GetSection("Jwt");
var key = Encoding.UTF8.GetBytes(jwtSettings["Key"]);
builder.Services.AddAuthentication(options => {
		   options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
		   options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
	   })
	   .AddJwtBearer(options => {
		   options.RequireHttpsMetadata = false; // Set to true in production
		   options.SaveToken = true;
		   options.TokenValidationParameters = new TokenValidationParameters {
			   ValidateIssuerSigningKey = true,
			   IssuerSigningKey = new SymmetricSecurityKey(key),
			   ValidateIssuer = true,
			   ValidIssuer = jwtSettings["Issuer"],
			   ValidateAudience = true,
			   ValidAudience = jwtSettings["Audience"],
			   ValidateLifetime = true,
			   ClockSkew = TimeSpan.Zero
		   };
	   });
builder.Services.AddAuthorization();
builder.Services.AddSwaggerGen(c => {
	c.SwaggerDoc("1.0.0",
				 new OpenApiInfo {
					 Version = "1.0.0",
					 Title = "Sample JWT Bearer Authentication Project",
					 Description = "Sample JWT Bearer Authentication Project .NET 9 Web API",
					 Contact = new OpenApiContact {
						 Name = "Swagger Codegen Contributors",
						 Url = new Uri("https://github.com/swagger-api/swagger-codegen"),
						 Email = "admin@admin.com"
					 },
					 TermsOfService = new Uri("https://www.example.com/policy")
				 });

	// Add JWT Authentication to Swagger
	c.AddSecurityDefinition("Bearer",
							new OpenApiSecurityScheme {
								Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
								Name = "Authorization",
								In = ParameterLocation.Header,
								Type = SecuritySchemeType.ApiKey,
								Scheme = "Bearer"
							});
	c.AddSecurityRequirement(new OpenApiSecurityRequirement {
		{
			new OpenApiSecurityScheme {
				Reference = new OpenApiReference {
					Type = ReferenceType.SecurityScheme,
					Id = "Bearer"
				}
			},
			[]
		}
	});
	c.CustomSchemaIds(type => type.FullName);
	c.IncludeXmlComments($"{AppContext.BaseDirectory}{Path.DirectorySeparatorChar}{builder.Environment.ApplicationName}.xml");
	c.OperationFilter<GeneratePathParamsValidationFilter>();
});
var app = builder.Build();
if (app.Environment.IsDevelopment()) {
	app.UseDeveloperExceptionPage();
}
else {
	app.UseExceptionHandler("/Error");
	app.UseHsts();
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseOutputCache();
app.UseAuthentication();
app.UseTokenExpirationValidation();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI(c => {
	c.SwaggerEndpoint("/swagger/1.0.0/swagger.json","JWT Bearer Authentication");
});
app.MapControllers();
app.MapDefaultEndpoints();
app.Run();
