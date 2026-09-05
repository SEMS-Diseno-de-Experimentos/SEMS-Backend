package com.sems.analytics.domain.model.valueobjects;

/**
 * Una posicion del ranking de consumo por dispositivo.
 *
 * <p>Value object inmutable. Se persiste embebido dentro del ranking, igual que
 * en el documento de MongoDB del servicio original.
 */
public record RankingItem(int rank, String deviceId, String deviceName, double totalKwh,
                          double estimatedAmount, double percentageOfTotal, String currency) {
}
