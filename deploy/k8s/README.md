# Manual Kubernetes manifests

Handout names map onto this platform as follows:

| Handout | This repository | DNS name |
|---------|-----------------|----------|
| catalog-api (products) | `Services/Catalog` contractor registry | `catalog-api` |
| orders-api | `Services/Tenders` procurement bidding | `tenders-api` |
| reviews-api | `Services/RiskAssessments` | `risk-assessments-api` |
| gateway | `Platform.ApiGateway` | `gateway` |

`/api/products` is the handout URL for a reconstruction work package. The gateway route `api-products` forwards it to `catalog-api`, and `ProductsController` also listens on `api/products`. A bearer token is still required (Lab 09).

Secrets are not stored in this directory. ConfigMaps hold only service addresses and non-secret settings. Passwords, connection strings, and `Jwt__SecretKey` are created with `kubectl` and mounted with `secretRef` / `secretKeyRef`.

Run only one of the two deployment styles at a time. `aspirate destroy` (or delete the namespace) before applying this folder, and the reverse before `aspirate apply`.

## Prerequisites

- Docker Desktop Kubernetes enabled (`kubectl config use-context docker-desktop`)
- Images built from the repository root (the cluster sees local images; `imagePullPolicy: IfNotPresent`):

```powershell
docker build -t catalog-api:v1 -f Services/Catalog/Catalog.Api/Dockerfile .
docker build -t gateway:v1 -f Platform.ApiGateway/Dockerfile .
```

The Dockerfiles are multi-stage: SDK image compiles, `dotnet restore` runs after the `.csproj` copy, the final image is `mcr.microsoft.com/dotnet/aspnet:10.0` and listens on 8080. The tag is 10.0 because every project targets `net10.0`.

## 1. Create secrets (values stay out of Git)

Pick the same SA password you already use for the SQL Server container, a RabbitMQ password, and the gateway JWT signing key from Lab 09 (`Jwt:SecretKey` in `Platform.ApiGateway` configuration). Do not write these values into a YAML file.

```powershell
$sa = Read-Host "SQL SA password"
$rabbit = Read-Host "RabbitMQ password"
$jwt = Read-Host "JWT signing key"

kubectl create secret generic sql-secrets --from-literal=SA_PASSWORD="$sa"

kubectl create secret generic messaging-secrets --from-literal=RABBITMQ_DEFAULT_PASS="$rabbit"

kubectl create secret generic catalog-secrets `
  --from-literal=ConnectionStrings__ContractorRegistry="Server=sql,1433;Database=ContractorRegistry;User Id=SA;Password=$sa;TrustServerCertificate=True" `
  --from-literal=ConnectionStrings__messaging="amqp://guest:${rabbit}@messaging:5672"

kubectl create secret generic gateway-secrets --from-literal=Jwt__SecretKey="$jwt"
```

`kubectl get secret gateway-secrets -o yaml` shows only Base64. That is encoding, not encryption.

Changing `gateway-config` does not affect a running pod until:

```powershell
kubectl rollout restart deploy/gateway
```

## 2. Apply

```powershell
kubectl apply -f deploy/k8s/configmap.yaml
kubectl apply -f deploy/k8s/sql-deployment.yaml -f deploy/k8s/sql-service.yaml
kubectl apply -f deploy/k8s/messaging-deployment.yaml -f deploy/k8s/messaging-service.yaml
kubectl apply -f deploy/k8s/redis-deployment.yaml -f deploy/k8s/redis-service.yaml
kubectl apply -f deploy/k8s/catalog-api-deployment.yaml -f deploy/k8s/catalog-api-service.yaml
kubectl apply -f deploy/k8s/gateway-deployment.yaml -f deploy/k8s/gateway-service.yaml
kubectl get pods,services
```

`catalog-api` runs EF migrations on startup, so it stays unready until `sql` and `messaging` accept connections. Two replicas is what the Deployment asks for; deleting one pod makes the Deployment create a replacement:

```powershell
kubectl delete pod -l app=catalog-api --field-selector=status.phase=Running --wait=false
kubectl get pods -l app=catalog-api -w
```

Stop SQL (scale to 0). The catalog process keeps running (`/health/live` is 200, the container is not restarted) but `/health/ready` returns 503, the pod becomes NotReady, and the Service stops sending it traffic:

```powershell
kubectl scale deploy/sql --replicas=0
kubectl describe pod -l app=catalog-api
kubectl scale deploy/sql --replicas=1
```

## 3. Call the gateway

```powershell
kubectl port-forward service/gateway 8080:80
```

`http://localhost:8080/api/products` enters the gateway Service (port 80), the gateway pod (port 8080), YARP route `api-products`, then DNS name `catalog-api` (Service port 80 to container port 8080). Without a bearer token the gateway answers 401. With a token that has `products:read`, catalog returns the work packages (ROAD, BLDG, ELEC).

```powershell
kubectl logs deploy/gateway | Select-String catalog
```

## How this differs from `aspirate-output`

| | Aspirate (`Platform.AppHost/aspirate-output`) | This folder |
|--|-----------------------------------------------|-------------|
| Scope | Whole platform: APIs, gateway, aggregator, SQL, MongoDB, Redis, RabbitMQ, Keycloak, IdentityServer | Gateway, catalog-api, plus SQL, RabbitMQ, and Redis so catalog can start |
| Layout | Kustomize, one directory per resource, `configMapGenerator` | Plain YAML files applied with `kubectl apply -f` |
| Image | `localhost:5001/<name>:latest` | `catalog-api:v1`, `gateway:v1` |
| Replicas | 1, strategy `Recreate` | 2 for gateway and catalog-api |
| Service port | 8080, same as the container | 80, `targetPort: 8080` |
| Probes | `/health/live` and `/health/ready` added on the .NET Deployments | Same paths, from Lab 07, registered in every environment |
| Secrets | Removed from the generated ConfigMaps and wired with `secretRef`. Placeholders in `manifest.json` stay as `{sql-password.value}` | Created only by the `kubectl create secret` commands above |

Aspirate was run from `Platform.AppHost`:

```powershell
aspirate init --non-interactive --disable-secrets --container-registry localhost:5001 --container-image-tag latest
aspirate generate --non-interactive --disable-secrets --skip-build --image-pull-policy IfNotPresent --include-dashboard false
```

`--disable-secrets` still inlined generated passwords into ConfigMap literals in aspirate 9.1.0. Those literals were removed before commit. `secretRef` names that replace them:

| Secret | Keys (not committed) |
|--------|----------------------|
| `sql-secrets` | `MSSQL_SA_PASSWORD` (Aspirate) or `SA_PASSWORD` (this folder) |
| `mongo-secrets` | `MONGO_INITDB_ROOT_PASSWORD` |
| `redis-secrets` | `REDIS_PASSWORD` |
| `messaging-secrets` | `RABBITMQ_DEFAULT_PASS` |
| `keycloak-secrets` | `KC_BOOTSTRAP_ADMIN_PASSWORD` |
| `catalog-secrets` | `ConnectionStrings__ContractorRegistry`, and for Aspirate also `ConnectionStrings__redis`, `ConnectionStrings__messaging` |
| `tenders-secrets` | `ConnectionStrings__ProcurementBidding`, `ConnectionStrings__redis`, `ConnectionStrings__messaging` |
| `risk-assessments-secrets` | `ConnectionStrings__RiskAssessmentDb`, `ConnectionStrings__redis`, `ConnectionStrings__messaging` |
| `auth-secrets` | `ConnectionStrings__IdentityDb` |
| `identityserver-secrets` | `ConnectionStrings__IdentityServerDb` |
| `aggregator-secrets` | `ConnectionStrings__redis` |
| `gateway-secrets` | `Jwt__SecretKey` |

To push images and apply the generated tree, run a local registry on port 5001, generate again **without** `--skip-build`, strip password literals the same way, create the secrets, then `kubectl apply -k Platform.AppHost/aspirate-output`. Service URLs in that tree use port 8080 (`http://catalog-api:8080`) because the generated Service port is 8080, not 80.
