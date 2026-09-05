package com.sems.energy.domain.services;

import com.sems.energy.domain.model.valueobjects.EnergyPrice;

/**
 * Puerto hacia el proveedor externo de tarifas electricas.
 *
 * <p>El dominio solo conoce esta interfaz. Hoy detras hay un adaptador
 * simulado; manana puede haber una integracion real sin tocar nada mas.
 */
public interface EnergyPricingProvider {
    EnergyPrice currentPrice();
}
