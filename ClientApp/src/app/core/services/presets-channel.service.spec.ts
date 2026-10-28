import { PresetsChannel } from './presets-channel.service';

describe('PresetsChannel', () => {
  let sender: PresetsChannel;
  let receiver: PresetsChannel;

  beforeEach(() => {
    sender = new PresetsChannel();
    receiver = new PresetsChannel();
  });

  afterEach(() => {
    sender.ngOnDestroy();
    receiver.ngOnDestroy();
  });

  it('should tell other instances (browser tabs) about a change', done => {
    receiver.changed$.subscribe(() => {
      expect().nothing();
      done();
    });

    sender.notifyChanged();
  });

  it('should not tell the sender itself', done => {
    let senderHeard = false;
    sender.changed$.subscribe(() => senderHeard = true);
    receiver.changed$.subscribe(() => setTimeout(() => {
      expect(senderHeard).toBeFalse();
      done();
    }, 20));

    sender.notifyChanged();
  });
});
