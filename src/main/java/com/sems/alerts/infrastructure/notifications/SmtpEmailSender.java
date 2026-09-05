package com.sems.alerts.infrastructure.notifications;

import com.sems.alerts.domain.services.EmailSender;
import lombok.RequiredArgsConstructor;
import lombok.extern.slf4j.Slf4j;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.mail.SimpleMailMessage;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.stereotype.Component;

/**
 * Envio de correo por SMTP.
 *
 * <p>Reemplaza al remitente de Gmail escrito a mano en Go. El interruptor
 * {@code app.mail.enabled} permite desactivar el envio en desarrollo sin tocar
 * codigo: el mensaje se registra en el log en lugar de salir.
 */
@Slf4j
@Component
@RequiredArgsConstructor
public class SmtpEmailSender implements EmailSender {

    private final JavaMailSender mailSender;

    @Value("${app.mail.from:}")
    private String from;

    @Value("${app.mail.enabled:true}")
    private boolean enabled;

    @Override
    public void send(String to, String subject, String body) {
        if (!enabled) {
            log.info("Correo desactivado; no se envia a {} con asunto '{}'", to, subject);
            return;
        }
        if (to == null || to.isBlank()) {
            throw new IllegalArgumentException("destinatario vacio");
        }

        SimpleMailMessage message = new SimpleMailMessage();
        if (from != null && !from.isBlank()) {
            message.setFrom(from);
        }
        message.setTo(to);
        message.setSubject(subject);
        message.setText(body);

        mailSender.send(message);
        log.info("Correo enviado a {} con asunto '{}'", to, subject);
    }
}
