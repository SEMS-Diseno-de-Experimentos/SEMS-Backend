# SEMS - Backend

Backend del **Smart Energy Management System**, escrito como **monolito modular
en ASP.NET Core 8 con C#**, tal como exige el enunciado del curso: *"Para el
desarrollo de Web Services... se hara uso de ASP.NET Core Framework, utilizando
C# como lenguaje de Programacion."*

---

## Por que un monolito modular y no microservicios

El diseno anterior eran ocho servicios independientes comunicandose por Kafka y
desplegados en Azure. Se unificaron en un solo proceso por tres razones:

1. **El enunciado no pide microservicios.** Pide un backend RESTful bien
   estructurado en bounded contexts, y eso se cumple igual dentro de un proceso.
2. **El costo de operacion desaparece.** Ya no hay broker, ni ocho despliegues,
   ni ocho bases de datos, ni la latencia de red entre servicios.
3. **Se conservo lo que si aportaba.** Cada bounded context sigue siendo un
   modulo con sus cuatro capas (`Domain`, `Application`, `Infrastructure`,
   `Interfaces`) y sus tablas separadas por prefijo. Los modulos se comunican por
   eventos de dominio, no llamandose los metodos entre si.

**Los topics de Kafka pasaron a ser tipos de evento.** `DomainEventBus` los
entrega dentro del proceso, y solo despues de que la transaccion que los origino
haya confirmado; nadie reacciona a un cambio que luego se revierte. Si algun dia
hace falta volver a un broker, el unico archivo que cambia es ese bus.

---

## Estructura

```
src/Sems.Api/
├── Program.cs                    arranque, inyeccion de dependencias, CORS, JWT
├── Shared/
│   ├── Configuration/DotEnv.cs   carga .env en desarrollo local
│   ├── Errors/                   AppException y el contrato JSON de error
│   ├── Events/                   DomainEvents + DomainEventBus
│   ├── Http/                     middleware que traduce excepciones a HTTP
│   └── Persistence/SemsDbContext.cs
└── Modules/
    ├── Iam/            identidad, autenticacion, tokens
    ├── Devices/        dispositivos y su vinculacion
    ├── Energy/         medidores, lecturas, consumo, tarifas
    ├── Analytics/      rankings, proyecciones, resumenes
    ├── Alerts/         umbrales, inactividad, notificaciones por correo
    ├── Payments/       cobros, medios de pago, comprobantes, webhook de Stripe
    └── Subscriptions/  planes y suscripciones
```

Cada modulo repite la misma division:

| Capa             | Que contiene                                                  |
|------------------|---------------------------------------------------------------|
| `Domain`         | entidades, objetos de valor y las interfaces de repositorio    |
| `Application`    | servicios de comando y consulta, manejadores de eventos        |
| `Infrastructure` | EF Core, Stripe, SMTP: todo lo que habla con el exterior       |
| `Interfaces`     | controladores y recursos (el contrato JSON)                    |

Las tablas llevan prefijo por modulo: `iam_`, `dm_`, `em_`, `an_`, `al_`, `pm_`,
`sb_`.

---

## El contrato JSON no es uniforme, y es a proposito

Los servicios originales estaban escritos en lenguajes distintos y cada uno
serializaba a su manera. El frontend ya esta escrito contra eso, asi que el
backend lo respeta campo por campo con `[JsonPropertyName]`:

| Modulo          | Peticiones   | Respuestas     |
|-----------------|--------------|----------------|
| Devices         | camelCase    | camelCase      |
| Energy          | snake_case   | snake_case     |
| Analytics       | snake_case   | snake_case     |
| Alerts          | snake_case   | snake_case     |
| Payments        | snake_case   | snake_case     |
| **Subscriptions** | **snake_case** | **PascalCase** |

La asimetria de Subscriptions viene de que el servicio en Go leia los cuerpos con
etiquetas pero devolvia las entidades sin etiquetar. Parece un error y es
exactamente el tipo de cosa que alguien "arregla" al leerla, asi que hay pruebas
que fallan si se toca (`SubscriptionContractTests`).

---

## Puesta en marcha

```bash
cp .env.example .env      # y rellenar los valores
dotnet restore
dotnet run --project src/Sems.Api
```

Queda escuchando en `http://localhost:8080`:

- `GET /health` estado del servicio
- `GET /metrics` metricas en formato Prometheus
- `GET /swagger` documentacion de la API

### Pruebas

```bash
dotnet test
```

33 pruebas. No necesitan base de datos: cubren el contrato JSON de los dos
modulos mas fragiles, las reglas de dinero de Payments, la evaluacion de umbrales
de Alerts y las garantias de seguridad de los tokens.

Con cobertura:

```bash
dotnet test --collect:"XPlat Code Coverage"
```

---

## Variables de entorno

Estan todas documentadas en `.env.example`. Las imprescindibles:

| Variable            | Para que                                              |
|---------------------|-------------------------------------------------------|
| `DATABASE_URL`      | cadena de conexion a Postgres                          |
| `JWT_SECRET`        | firma de los tokens; minimo 32 caracteres              |
| `ALLOWED_ORIGINS`   | origenes permitidos por CORS, separados por coma       |
| `STRIPE_SECRET_KEY` | cobros                                                 |
| `MAIL_USERNAME` / `MAIL_PASSWORD` | correos de verificacion y alertas     |

---

## Seguridad

Cuatro decisiones que conviene no deshacer por descuido:

- **`.env` esta en `.gitignore` y nunca debe subirse.** Lleva la clave secreta de
  Stripe, la contrasena del SMTP y la de la base de datos. Si una se sube por
  error, borrarla del repositorio no basta: hay que rotarla en el proveedor.
- **Los tokens se guardan como resumen SHA-256, jamas en claro.** Una copia de la
  base de datos no permite suplantar a nadie.
- **`forgot-password` responde igual exista o no el correo.** Si respondiera
  distinto, cualquiera podria averiguar que direcciones estan registradas.
- **El webhook de Stripe no lleva JWT y no debe llevarlo.** Se autentica con la
  firma del cuerpo; exigirle un token haria que Stripe no pudiera entregarlo.

Ninguna clave secreta debe llevar el prefijo `VITE_`: ese prefijo hace que Vite
la incruste en el paquete que se descarga el navegador.
