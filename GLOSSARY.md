# TauTick research

TauTick connects discretionary observation of recorded markets with repeatable experiments on the same events.

## Language

**Workspace**:
The complete arrangement of TauTick's windows and their contents. One workspace is active at a time; a named saved layout records that arrangement.
_Avoid_: Workspace tab for a content tab

**Content tab**:
A named page in a workspace window that contains an independent chart layout or is empty. Moving a content tab carries its contents and settings to its receiving window.


**Bar**:
A summary of market activity over a defined interval, identified by its instrument and interval boundaries. Its open, high, low, close and volume can be supplemented by executed volume at individual prices.

**Candle**:
A visual representation of a bar's open, high, low and close prices.

**Footprint**:
A price-level breakdown of executed volume within a bar, often separated by trade aggressor side. It describes executions rather than resting liquidity.
_Avoid_: Order book when referring to executed volume inside a bar

**Chart viewport**:
The time or bar range and price range currently visible on the chart.

**Semantic zoom**:
A change in chart representation as the available display space changes, such as revealing a bar's footprint when zooming in. The underlying bar interval and market data remain the same.

**Recorded session**:
A concrete futures contract's recorded market activity within a named trading session. Its session boundaries and available history are part of its identity.
_Avoid_: Day when the intended meaning is a trading session spanning calendar dates

**Research interval**:
A trader-selected span within a recorded session, optionally accompanied by a description of the behavior worth investigating. Selection alone does not arm Entry Trigger.
_Avoid_: Trade, signal, opportunity when referring only to a selected span

**Experiment**:
A saved research interval together with its replay setup, manual actions, configurations, and associated results. It is the unit a trader reopens to continue an investigation.
_Avoid_: Run when referring to the whole saved investigation

**Configuration**:
The complete settings used to evaluate an experiment. An alternative configuration changes declared settings while retaining the same market inputs and manual actions for comparison.

**Run**:
One evaluation of an experiment under a particular configuration. A run can finish without producing a signal or be incomplete because required evidence is unavailable.
_Avoid_: Experiment when referring to a single evaluation

**Arming action**:
The trader's instruction to consider an entry in a chosen direction at a specific point in replay. The action and its acceptance are distinct: a configuration can reject the same instruction another accepts.

**Armed attempt**:
One accepted arming action and its subsequent Entry Trigger evaluation through a terminal outcome. One experiment may retain multiple attempts without treating them as independent market samples.

**Candidate**:
Entry Trigger's evidence that approaching aggression has become ineffective or momentum has faded. A candidate can exist before arming; it retains its original age and validity when adopted by an attempt.

**Confirmation**:
The fresh directional response required after permission becomes active and a candidate qualifies or is adopted. Its precise conditions belong to the versioned Entry Trigger detector contract.

**Signal**:
Entry Trigger's conclusion that its configured entry conditions have been met. A signal does not establish that an order was submitted or filled.
_Avoid_: Fill, executed trade

**Warmup**:
Processing preceding history to establish the state required at a replay starting point. It includes relevant session context and pre-arm candidate history, not only the visible interval.

**Historical playback**:
Presentation of recorded events at their recorded pace or a user-selected speed. Pausing or changing viewing speed does not change the experiment's recorded timing.
_Avoid_: Real time when it could mean the current live market
