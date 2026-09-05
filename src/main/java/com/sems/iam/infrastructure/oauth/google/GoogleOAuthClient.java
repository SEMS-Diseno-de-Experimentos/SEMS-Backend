package com.sems.iam.infrastructure.oauth.google;

import java.util.Map;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.http.MediaType;
import org.springframework.stereotype.Component;
import org.springframework.util.LinkedMultiValueMap;
import org.springframework.util.MultiValueMap;
import org.springframework.web.client.RestClient;

@Component
public class GoogleOAuthClient {
    private final RestClient restClient = RestClient.builder()
            .baseUrl("https://oauth2.googleapis.com")
            .build();

    @Value("${security.oauth2.google.client-id:}")
    private String clientId;

    @Value("${security.oauth2.google.client-secret:}")
    private String clientSecret;

    @Value("${security.oauth2.google.redirect-uri:}")
    private String redirectUri;

    public String exchangeCodeForIdToken(String code) {
        MultiValueMap<String, String> form = new LinkedMultiValueMap<>();
        form.add("code", code);
        form.add("client_id", clientId);
        form.add("client_secret", clientSecret);
        form.add("redirect_uri", redirectUri);
        form.add("grant_type", "authorization_code");

        Map<String, Object> tokenResponse = restClient.post()
                .uri("/token")
                .contentType(MediaType.APPLICATION_FORM_URLENCODED)
                .body(form)
                .retrieve()
                .body(Map.class);

        Object idToken = tokenResponse == null ? null : tokenResponse.get("id_token");
        return idToken == null ? "" : idToken.toString();
    }
}
