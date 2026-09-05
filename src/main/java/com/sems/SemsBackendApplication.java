package com.sems;

import org.springframework.boot.SpringApplication;
import org.springframework.boot.autoconfigure.SpringBootApplication;
import org.springframework.retry.annotation.EnableRetry;
import org.springframework.scheduling.annotation.EnableAsync;
import org.springframework.scheduling.annotation.EnableScheduling;

/**
 * SEMS - Monolito modular.
 *
 * <p>Un unico desplegable que contiene los bounded contexts del sistema como
 * modulos independientes bajo {@code com.sems.<modulo>}, cada uno con sus capas
 * domain, application, infrastructure e interfaces.
 *
 * <p>{@code @EnableAsync} permite que los consumidores de eventos de dominio
 * corran fuera del hilo de la peticion, igual que hacian los consumidores de
 * Kafka. {@code @EnableScheduling} sostiene las tareas periodicas (detector de
 * inactividad, cierres de periodo). {@code @EnableRetry} reemplaza el backoff
 * escrito a mano del envio de correos.
 */
@EnableAsync
@EnableRetry
@EnableScheduling
@SpringBootApplication
public class SemsBackendApplication {

    public static void main(String[] args) {
        SpringApplication.run(SemsBackendApplication.class, args);
    }
}
