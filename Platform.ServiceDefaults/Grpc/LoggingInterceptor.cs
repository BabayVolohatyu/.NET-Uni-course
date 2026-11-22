// This file intentionally left as a placeholder.
// gRPC LoggingInterceptor implementations live in each service's Grpc/ folder:
//   Services/Tenders/Tenders.Api/Grpc/LoggingInterceptor.cs
//   Services/Catalog/Catalog.Api/Grpc/LoggingInterceptor.cs
//   Services/RiskAssessments/RiskAssessments.WebApi/Grpc/LoggingInterceptor.cs
// Each service registers it via: builder.Services.AddGrpc(o => o.Interceptors.Add<LoggingInterceptor>())
