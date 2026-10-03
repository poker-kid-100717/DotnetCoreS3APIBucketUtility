import { Container } from "@cloudflare/containers";

export interface Env {
  API: DurableObjectNamespace<S3UtilityApi>;
  R2_ENDPOINT: string;
  R2_ACCESS_KEY_ID: string;
  R2_SECRET_ACCESS_KEY: string;
  DATABASE_URL?: string;
  API_KEY?: string;
}

// The ASP.NET Core API (and its architecture page at /) runs in the container. Objects live in
// Cloudflare R2 through its S3-compatible API; object metadata goes to MongoDB when DATABASE_URL is set.
export class S3UtilityApi extends Container<Env> {
  defaultPort = 8080;
  sleepAfter = "10m";
  pingEndpoint = "localhost/health";

  constructor(ctx: DurableObjectState<{}>, env: Env) {
    super(ctx, env);
    this.envVars = {
      ASPNETCORE_ENVIRONMENT: "Production",
      Storage__ServiceUrl: env.R2_ENDPOINT,
      Storage__AccessKeyId: env.R2_ACCESS_KEY_ID,
      Storage__SecretAccessKey: env.R2_SECRET_ACCESS_KEY,
      Storage__Region: "auto",
      ...(env.DATABASE_URL ? { DATABASE_URL: env.DATABASE_URL } : {}),
      // A public URL must not let anyone write to the bucket: writes need X-Api-Key, and are refused without API_KEY.
      Api__RequireKey: "true",
      ...(env.API_KEY ? { Api__Key: env.API_KEY } : {}),
    };
  }
}

export default {
  async fetch(request: Request, env: Env): Promise<Response> {
    return env.API.getByName("api").fetch(request);
  },
} satisfies ExportedHandler<Env>;
