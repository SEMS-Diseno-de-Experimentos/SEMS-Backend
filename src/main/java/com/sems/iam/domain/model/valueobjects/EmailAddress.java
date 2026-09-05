package com.sems.iam.domain.model.valueobjects;

import java.util.Locale;
import java.util.regex.Pattern;

/**
 * EmailAddress is a "value object": a small immutable type that represents a
 * concept by its value, not by an identity. Two EmailAddress objects with the
 * same text are considered equal.
 *
 * It is a Java "record", which automatically gives us the constructor, the
 * getter (value()), equals(), hashCode() and toString(). Wrapping the email in
 * its own type (instead of passing a raw String around) means the email can
 * NEVER exist in an invalid form: if you hold an EmailAddress, it is guaranteed
 * to be valid.

/**
 * Objeto de Valor que representa una dirección de correo electrónico.
 * 
 * EmailAddress es un record de Java que encapsula la lógica de validación
 * y normalización de direcciones de correo electrónico dentro del dominio IAM.
 * 
 * Esta clase implementa el patrón Value Object del Domain-Driven Design (DDD),
 * asegurando que solo se pueden crear instancias válidas de EmailAddress.
 * 
 * @author SEMS IAM Service
 * @version 1.0
 * @since 1.0

 */
public record EmailAddress(String value) {
    // A compiled regular expression used to check the basic email shape. It is
    // static + final so the pattern is compiled only once and shared by all
    // instances (compiling a regex repeatedly would be wasteful).
    private static final Pattern EMAIL_PATTERN = Pattern.compile("^[A-Za-z0-9+_.-]+@[A-Za-z0-9.-]+$");

    /**
     * This is a "compact constructor" of the record. It runs every time an
     * EmailAddress is created and lets us validate and normalise the value:
     *  - First we reject null, blank, or badly formatted emails.
     *  - Then we trim spaces and lower-case it so that "User@Mail.com" and
     *    "user@mail.com  " are stored the same way (important for uniqueness).
     */
    public EmailAddress {
        if (value == null || value.isBlank() || !EMAIL_PATTERN.matcher(value).matches()) {
            throw new IllegalArgumentException("Invalid email address");
        }
        // Locale.ROOT makes lower-casing behave the same on every machine,
        // avoiding locale-specific surprises (e.g. the Turkish "i" problem).
        value = value.trim().toLowerCase(Locale.ROOT);
    }
}
