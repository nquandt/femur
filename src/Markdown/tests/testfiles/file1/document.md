---
title: "Client-Side Redirect"
slug: "client-side-redirect"
lang: en
description: "C# extension method for HttpContext to perform client-side redirects using meta-refresh."
---

If ever needing to force cookie attachment in a user flow given "strict" cookie safesite settings.

:::C:Codeblock {lang="csharp" filename="ClientSideRedirectExtensions.cs" rawUrl="~/files/articles/ClientSideRedirectExtensions.cs"}
:::

This does mess with client side analytics tools that rely on things like url referrer headers since the redirect now happens entirely on the client side. You also would need to be cautious about traffic metrics as this would look like an extra visit in many cases.

## Notes & Alternatives

- Prevent caching: call a small helper `context.PreventCaching()` before writing the redirect HTML so browsers and proxies won't cache the ephemeral redirect page. Example helper:

:::C:Codeblock {lang="csharp"}
public static void PreventCaching(this HttpContext context)
{
    var headers = context.Response.Headers;
    headers["Cache-Control"] = "no-store, no-cache, must-revalidate, proxy-revalidate";
    headers["Pragma"] = "no-cache";
    headers["Expires"] = "0";
    headers["Surrogate-Control"] = "no-store";
}
:::

- Accessibility / UX: always include a readable message and a visible link for cases where users might happen to see this page even by accident. Consider `role="status"` or simple text so screen readers announce the state.

- Analytics & referrers: client-side redirects (meta-refresh or JS) typically look like an extra page view in analytics and may not preserve referrer headers the same way as server-side 3xx redirects. If accurate analytics or referrer delivery is critical, prefer server-side redirects when SameSite/cookie configuration allows it.

  - You might also be able to include a special query parameter in the redirect URL to indicate the source of the redirect for analytics purposes.

- Cookie / SameSite guidance: this technique is intended as a fallback when cookies are blocked by SameSite=Strict in cross-site flows. If acceptable, you might be better off setting cookies with `SameSite=None; Secure` over relying on client-side redirects, provided you understand the security implications and are serving over HTTPS.

- CSP/security: There are discussions being had about allowing CSP to control the user of meta-refresh. This functionality might break for websites with strict CSP if that is implemented. Also note that if you are enabling client side redirect tactics you should evaluate all forms of XSRF and XSS protections.

## Example usage

Call the helper from middleware or from a controller action when you have a cross-site cookie requirement:

:::C:Codeblock {lang="csharp"}
await context.ResponseWithClientSideRedirectAsync("/dashboard");
:::

Recommendation: Use sparingly, and prefer server side redirects in all other cases.