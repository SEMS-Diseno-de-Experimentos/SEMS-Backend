package com.sems.analytics.domain.model.entities;

import com.sems.analytics.domain.model.valueobjects.RankingItem;
import java.time.Instant;
import java.util.List;
import java.util.UUID;
import lombok.Getter;

/** Ordenacion de los dispositivos de un usuario por consumo en un periodo. */
@Getter
public class ConsumptionRanking {

    private final UUID id;
    private final String userId;
    private final String periodType;
    private final Instant periodStart;
    private final Instant periodEnd;
    private final List<RankingItem> rankings;
    private final Instant generatedAt;
    private final Instant createdAt;

    public ConsumptionRanking(UUID id, String userId, String periodType, Instant periodStart,
                              Instant periodEnd, List<RankingItem> rankings,
                              Instant generatedAt, Instant createdAt) {
        this.id = id;
        this.userId = userId;
        this.periodType = periodType;
        this.periodStart = periodStart;
        this.periodEnd = periodEnd;
        this.rankings = rankings == null ? List.of() : List.copyOf(rankings);
        this.generatedAt = generatedAt;
        this.createdAt = createdAt;
    }

    public static ConsumptionRanking create(String userId, String periodType, Instant periodStart,
                                            Instant periodEnd, List<RankingItem> rankings) {
        Instant now = Instant.now();
        return new ConsumptionRanking(UUID.randomUUID(), userId, periodType, periodStart,
                periodEnd, rankings, now, now);
    }
}
