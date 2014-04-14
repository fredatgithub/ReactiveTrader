﻿interface IExecutionRepository {
    execute(executablePrice: IExecutablePrice, notional: number, dealtCurrency: string)
        : Rx.Observable<IStale<ITrade>>;
} 